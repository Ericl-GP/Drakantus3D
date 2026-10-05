using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Colecionável (Resources/Data/collectibles.json). kind: "estrela" (Fragmento de Estrela) | "livro" (Livro de Lore,
    /// libera a página "lore" do Códex) | "moeda" (Moeda antiga). minFloor = andar mínimo onde pode aparecer.</summary>
    [System.Serializable]
    public class CollectibleDef
    {
        public string id, kind, name, desc, lore;
        public int minFloor = 1, coins, xp;
    }

    [System.Serializable] class CollectibleList { public CollectibleDef[] collectibles; }

    /// <summary>
    /// Colecionáveis da Torre: a cada andar visitado aparecem 1–3 ainda não coletados, em pontos livres do chão,
    /// com brilho girando e feixe de luz. Coletados ficam em GuildState.G.collected (nunca se repetem).
    /// Disparado por Pets.OnTravel → Collectibles.OnTravel.
    /// </summary>
    public static class Collectibles
    {
        public static readonly List<CollectibleDef> All = new();
        static readonly Dictionary<string, CollectibleDef> byId = new();
        static bool loaded;

        static GuildProfile G => GuildState.G;

        public static readonly string[] Kinds = { "estrela", "livro", "moeda" };

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            var ta = Resources.Load<TextAsset>("Data/collectibles");
            if (ta == null) { Debug.LogWarning("[Drakantus] Faltando Resources/Data/collectibles.json"); return; }
            CollectibleList l = null;
            try { l = JsonUtility.FromJson<CollectibleList>(ta.text); }
            catch (System.Exception ex) { Debug.LogError("[Drakantus] Erro lendo collectibles.json: " + ex.Message); }
            if (l == null || l.collectibles == null) return;
            foreach (var c in l.collectibles)
            {
                if (c == null || string.IsNullOrEmpty(c.id) || byId.ContainsKey(c.id)) continue;
                if (string.IsNullOrEmpty(c.kind)) c.kind = "estrela";
                if (c.minFloor <= 0) c.minFloor = 1;
                byId[c.id] = c;
                All.Add(c);
            }
        }

        public static CollectibleDef Def(string id) { Load(); return id != null && byId.TryGetValue(id, out var d) ? d : null; }
        public static bool Has(string id) => G.collected.Contains(id);
        public static int Total { get { Load(); return All.Count; } }

        /// <summary>Quantos colecionáveis deste tipo existem (owned = false) ou já foram coletados (owned = true).</summary>
        public static int CountKind(string kind, bool owned)
        {
            Load();
            int n = 0;
            for (int i = 0; i < All.Count; i++)
                if (All[i].kind == kind && (!owned || Has(All[i].id))) n++;
            return n;
        }

        public static string KindName(string kind)
        {
            switch (kind)
            {
                case "estrela": return "Fragmentos de Estrela";
                case "livro": return "Livros de Lore";
                case "moeda": return "Moedas Antigas";
            }
            return "Curiosidades";
        }

        public static Color KindColor(string kind)
        {
            switch (kind)
            {
                case "estrela": return U.Hex("ffe98a");
                case "livro": return U.Hex("c88aff");
                case "moeda": return U.Hex("ffaa55");
            }
            return Color.white;
        }

        // ------------------------------------------------------------------ surgimento
        /// <summary>Chamado (via Pets.OnTravel) depois que o herói chega a um mapa: espalha 1–3 colecionáveis nos andares da Torre.</summary>
        public static void OnTravel()
        {
            var g = Game.I;
            if (g == null || g.level == null || g.levelRoot == null || g.player == null) return;
            if (!g.InDungeon || g.level.floor <= 0) return;
            Load();
            if (All.Count == 0) return;
            var go = new GameObject("Colecionaveis");
            go.transform.SetParent(g.levelRoot, false);
            var sp = go.AddComponent<CollectibleSpawner>();
            sp.floor = g.level.floor;
        }

        /// <summary>Até "max" colecionáveis ainda não coletados que podem aparecer neste andar (sem repetir).</summary>
        public static List<CollectibleDef> PickFor(int floor, int max)
        {
            Load();
            var pool = new List<CollectibleDef>();
            foreach (var c in All) if (!Has(c.id) && c.minFloor <= floor) pool.Add(c);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var t = pool[i]; pool[i] = pool[j]; pool[j] = t;
            }
            // os do próprio andar primeiro
            pool.Sort((a, b) => (b.minFloor == floor ? 1 : 0).CompareTo(a.minFloor == floor ? 1 : 0));
            if (pool.Count > max) pool.RemoveRange(max, pool.Count - max);
            return pool;
        }

        // ------------------------------------------------------------------ coleta
        public static void Collect(CollectibleDef d, Vector3 pos)
        {
            if (d == null || Has(d.id)) return;
            G.collected.Add(d.id);
            G.stats.collected++;
            Color c = KindColor(d.kind);
            if (d.coins > 0) GameState.AddCoins(d.coins);
            if (d.xp > 0) GameState.AddXp(d.xp);
            string extra = "";
            if (d.coins > 0) extra += "  +" + d.coins + " moedas";
            if (d.xp > 0) extra += "  +" + d.xp + " XP";
            int have = CountKind(d.kind, true), total = CountKind(d.kind, false);
            GameState.Notify("Coleção: " + d.name + "  (" + KindName(d.kind) + " " + have + "/" + total + ")" + extra);
            HUD.Popup(pos + Vector3.up * 1.2f, d.name, c, true);
            FX.Pillar(pos, c, 0.9f);
            FX.Sparkle(pos + Vector3.up * 0.8f, c, 0.6f, 0.6f);
            Sfx.Play("item_rare", pos, 0.9f);
            if (!string.IsNullOrEmpty(d.lore)) Lore.Unlock(d.lore, true);
            Lore.CheckUnlocks(true);
            GuildState.Save();
            GuildState.Emit();
            Achievements.Check();
        }
    }

    // ======================================================================
    /// <summary>Espera o andar assentar (física pronta) e espalha os colecionáveis em chão livre.</summary>
    public class CollectibleSpawner : MonoBehaviour
    {
        public int floor = 1;

        IEnumerator Start()
        {
            yield return new WaitForSeconds(0.6f);
            var g = Game.I;
            if (g == null || g.level == null || g.player == null) yield break;
            int want = Random.Range(1, 4);
            var picks = Collectibles.PickFor(floor, want);
            if (picks.Count == 0) yield break;
            Physics.SyncTransforms();
            Vector3 hero = g.player.transform.position;
            var used = new List<Vector3>();
            foreach (var d in picks)
            {
                Vector3 spot;
                if (!FindSpot(g.level.bounds, hero, used, out spot)) continue;
                used.Add(spot);
                CollectiblePickup.Create(d, spot, transform);
            }
        }

        static bool FindSpot(Bounds b, Vector3 hero, List<Vector3> used, out Vector3 spot)
        {
            Vector3 mn = b.min, mx = b.max;
            for (int i = 0; i < 60; i++)
            {
                var c = new Vector3(Random.Range(mn.x + 2f, mx.x - 2f), hero.y, Random.Range(mn.z + 2f, mx.z - 2f));
                Vector3 g;
                if (!EnemyAbilities.GroundAt(c, hero.y, out g) || EnemyAbilities.Blocked(g)) continue;
                if (U.Flat(g - hero).magnitude < (i < 40 ? 10f : 5f)) continue;
                bool near = false;
                foreach (var u in used) if (U.Flat(g - u).magnitude < 6f) { near = true; break; }
                if (near) continue;
                spot = g;
                return true;
            }
            // reserva: ao redor do herói
            if (RandomEvents.FindSpot(hero, 6f, 22f, out spot, false))
            {
                foreach (var u in used) if (U.Flat(spot - u).magnitude < 2f) return false;
                return true;
            }
            return false;
        }
    }

    // ======================================================================
    /// <summary>Colecionável no chão: forma girando e flutuando, anel de brilho girando, feixe de luz e luz pontual.
    /// O herói coleta ao encostar (1,4 m).</summary>
    public class CollectiblePickup : MonoBehaviour
    {
        public CollectibleDef def;
        Transform spin, ring, beam;
        float t, baseY;
        bool taken;
        Light lightC;
        Color col;

        public static CollectiblePickup Create(CollectibleDef d, Vector3 pos, Transform parent)
        {
            var go = new GameObject("Colecionavel_" + d.id);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var c = go.AddComponent<CollectiblePickup>();
            c.def = d;
            c.baseY = pos.y;
            c.t = Random.value * 10f;
            c.Build();
            return c;
        }

        void Build()
        {
            col = Collectibles.KindColor(def.kind);
            var v = new GameObject("visual").transform;
            v.SetParent(transform, false);
            v.localPosition = new Vector3(0f, 0.9f, 0f);
            spin = v;
            switch (def.kind)
            {
                case "livro":
                {
                    var cover = U.Prim(PrimitiveType.Cube, v, Vector3.zero, new Vector3(0.46f, 0.12f, 0.34f), U.Hex("5a2a7a"));
                    cover.GetComponent<Renderer>().sharedMaterial = U.Lit(U.Hex("5a2a7a"), 0.6f, col * 0.5f);
                    var pages = U.Prim(PrimitiveType.Cube, v, new Vector3(0.02f, 0f, 0f), new Vector3(0.43f, 0.09f, 0.31f), U.Hex("f4ead2"));
                    pages.GetComponent<Renderer>().sharedMaterial = U.Lit(U.Hex("f4ead2"), 0.3f, U.Hex("f4ead2") * 0.3f);
                    var clasp = U.Prim(PrimitiveType.Cube, v, new Vector3(-0.23f, 0f, 0f), new Vector3(0.04f, 0.14f, 0.36f), U.Hex("ffd04a"));
                    clasp.GetComponent<Renderer>().sharedMaterial = U.Lit(U.Hex("ffd04a"), 0.9f, U.Hex("ffd04a") * 0.6f);
                    v.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                    break;
                }
                case "moeda":
                {
                    var coin = U.Prim(PrimitiveType.Cylinder, v, Vector3.zero, new Vector3(0.46f, 0.035f, 0.46f), col);
                    coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    coin.GetComponent<Renderer>().sharedMaterial = U.Lit(U.Hex("c8843a"), 0.85f, col * 0.6f);
                    var inner = U.Prim(PrimitiveType.Cylinder, coin.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.62f, 0.4f, 0.62f), U.Hex("6fae8a"));
                    inner.GetComponent<Renderer>().sharedMaterial = U.Lit(U.Hex("6fae8a"), 0.7f, U.Hex("3f8a6a") * 0.4f);
                    var mark = U.Prim(PrimitiveType.Cube, coin.transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.22f, 0.4f, 0.22f), U.Hex("ffd27a"));
                    mark.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    mark.GetComponent<Renderer>().sharedMaterial = U.Lit(U.Hex("ffd27a"), 0.9f, U.Hex("ffd27a") * 0.5f);
                    break;
                }
                default: // estrela
                {
                    var mat = U.Lit(col, 0.9f, col * 1.6f);
                    var a = U.Prim(PrimitiveType.Cube, v, Vector3.zero, Vector3.one * 0.3f, col);
                    a.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                    a.GetComponent<Renderer>().sharedMaterial = mat;
                    var b = U.Prim(PrimitiveType.Cube, v, Vector3.zero, Vector3.one * 0.3f, col);
                    b.transform.localRotation = Quaternion.Euler(0f, 45f, 45f);
                    b.GetComponent<Renderer>().sharedMaterial = mat;
                    var core = U.Prim(PrimitiveType.Sphere, v, Vector3.zero, Vector3.one * 0.22f, Color.white);
                    core.GetComponent<Renderer>().sharedMaterial = U.Lit(Color.white, 0.9f, Color.white * 1.2f);
                    break;
                }
            }

            // feixe de luz vertical
            var bm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(bm.GetComponent<Collider>());
            bm.name = "feixe";
            bm.transform.SetParent(transform, false);
            bm.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            bm.transform.localScale = new Vector3(0.28f, 2.6f, 0.28f);
            var br = bm.GetComponent<Renderer>();
            br.sharedMaterial = U.Fx(true);
            FX.SetColor(br, new Color(col.r, col.g, col.b, 0.32f));
            beam = bm.transform;

            // anel de brilho girando no chão + brilho suave
            var rq = FX.FlatQuad("anel", transform.position, U.RingTexture(), new Color(col.r, col.g, col.b, 0.8f), true);
            rq.transform.SetParent(transform, false);
            rq.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            rq.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rq.transform.localScale = Vector3.one * 1.5f;
            ring = rq.transform;
            var gq = FX.FlatQuad("brilho", transform.position, U.SoftTexture(), new Color(col.r, col.g, col.b, 0.5f), true);
            gq.transform.SetParent(transform, false);
            gq.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            gq.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            gq.transform.localScale = Vector3.one * 2.4f;

            var lg = new GameObject("luz");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            lightC = lg.AddComponent<Light>();
            lightC.type = LightType.Point; lightC.color = col; lightC.intensity = 2f; lightC.range = 4f; lightC.shadows = LightShadows.None;

            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        void Update()
        {
            if (taken) return;
            float dt = Time.deltaTime;
            t += dt;
            if (spin != null)
            {
                spin.Rotate(0f, 110f * dt, 0f, Space.World);
                var lp = spin.localPosition;
                lp.y = 0.9f + Mathf.Sin(t * 2.2f) * 0.12f;
                spin.localPosition = lp;
            }
            if (ring != null) ring.Rotate(0f, 0f, 60f * dt, Space.Self);
            if (beam != null)
            {
                float k = 1f + Mathf.Sin(t * 3f) * 0.12f;
                beam.localScale = new Vector3(0.28f * k, 2.6f, 0.28f * k);
            }
            if (lightC != null) lightC.intensity = 1.7f + Mathf.Sin(t * 4f) * 0.4f;

            var g = Game.I;
            var p = g != null ? g.player : null;
            if (p == null || p.state == "dead" || g.Traveling) return;
            if (U.Flat(p.transform.position - transform.position).magnitude <= 1.4f && Mathf.Abs(p.transform.position.y - transform.position.y) < 2.5f)
            {
                taken = true;
                Collectibles.Collect(def, transform.position);
                Destroy(gameObject);
            }
        }
    }
}
