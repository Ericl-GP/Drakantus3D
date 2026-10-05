using UnityEngine;
using D = Drakantus.LevelDecor;

namespace Drakantus
{
    /// <summary>
    /// [Praca] Praça de Aster em vários níveis (chamada pelo LevelBuilder.BuildTown):
    ///  - Praça baixa (y 0) com rosa-dos-ventos, poço, bancos e canteiros.
    ///  - Terraço da Fonte (y 1,5) com chafariz: escadaria larga ao sul, escadas ao norte (Torre) e a leste,
    ///    rampa a oeste (ferreiro/mirante).
    ///  - Mirante (Torre de Vigia) com escada em caracol quadrado (3 lances de 2 m) até o topo a 6 m.
    ///  - Adarve na muralha sul (y 3,35), dos dois lados do portão, com escadas encostadas no muro.
    ///  - Canal com água (bordas elevadas) e ponte em arco no caminho da Guilda.
    ///  - Deck do mercado (y 0,75), deck da taverna (y 1,0), sacada da Guilda (y 2,0) e duas colinas gramadas.
    /// COLISÃO: degraus são só visuais; embaixo de cada lance há UMA rampa invisível (BoxCollider inclinado)
    /// que passa no meio dos espelhos (inclinação ≈ 24–29°, abaixo do slopeLimit 50 do herói; desvio
    /// de ±12 cm em relação aos degraus). Plataformas têm um BoxCollider único com o topo exato.
    /// Parapeitos/corrimãos têm colisor de 1,6 m (não dá para cair sem querer). O herói não pula.
    /// </summary>
    public static class TownPlaza
    {
        // ------------------------------------------------------------------ alturas andáveis (m)
        public const float TERRACE_TOP = 1.5f;
        public const float MARKET_TOP = 0.75f;
        public const float TAVERN_TOP = 1.0f;
        public const float BALCONY_TOP = 2.0f;
        public const float RAMPART_TOP = 3.35f;
        public const float LOOKOUT_TOP = 6f;
        public const float BRIDGE_TOP = 0.85f;

        public static readonly Vector3 FOUNTAIN = new Vector3(0f, TERRACE_TOP, 6f);
        /// <summary>Base (chão) do centro do Mirante.</summary>
        public static readonly Vector3 LOOKOUT = new Vector3(-10f, 0f, 19.5f);
        /// <summary>Ponto livre no topo do Mirante (longe do braseiro e da bandeira).</summary>
        public static Vector3 LookoutTop => new Vector3(LOOKOUT.x - 1.3f, LOOKOUT_TOP, LOOKOUT.z + 0.2f);

        const float LK_A = 2.2f;   // meio-lado do núcleo do Mirante
        const float LK_W = 1.8f;   // largura dos lances

        const int RAIL_NONE = 0, RAIL_L = 1, RAIL_R = 2, RAIL_BOTH = 3;

        // ------------------------------------------------------------------ cores (multiplicam as texturas)
        static readonly Color STONE_WALL = new Color(0.82f, 0.76f, 0.68f);
        static readonly Color STONE_GREY = new Color(0.70f, 0.69f, 0.70f);
        static readonly Color CAP_STONE = new Color(0.92f, 0.88f, 0.80f);
        static readonly Color TILE_TERRACE = new Color(0.94f, 0.86f, 0.75f);
        static readonly Color TILE_PLAZA = new Color(0.80f, 0.76f, 0.70f);
        static readonly Color TILE_ROSE = new Color(0.88f, 0.64f, 0.54f);
        static readonly Color TILE_SLATE = new Color(0.58f, 0.62f, 0.68f);
        static readonly Color STEP_STONE = new Color(0.86f, 0.82f, 0.75f);
        static readonly Color WOOD = new Color(0.66f, 0.47f, 0.30f);
        static readonly Color WOOD_DARK = new Color(0.38f, 0.26f, 0.17f);
        static readonly Color BEAM = new Color(0.30f, 0.21f, 0.14f);
        static readonly Color WATER = new Color(0.20f, 0.50f, 0.62f);
        static readonly Color WATER_GLOW = new Color(0.03f, 0.10f, 0.13f);
        static readonly Color IVY = new Color(0.25f, 0.46f, 0.22f);
        static readonly Color IVY_DARK = new Color(0.16f, 0.33f, 0.17f);
        static readonly Color GRASS_HILL = new Color(0.43f, 0.62f, 0.33f);   // = GRASS do LevelBuilder (emenda invisível)
        static readonly Color DIRT = new Color(0.52f, 0.41f, 0.28f);
        static readonly Color LANTERN_GLOW = new Color(1f, 0.82f, 0.45f);
        static readonly Color SLIT = new Color(0.10f, 0.09f, 0.10f);
        static readonly Color[] FLOWERS = { new Color(0.91f, 0.34f, 0.42f), new Color(0.95f, 0.78f, 0.29f), new Color(0.69f, 0.48f, 0.85f), new Color(0.96f, 0.94f, 0.90f), new Color(1f, 0.54f, 0.30f) };
        static readonly string[] BUSHES = { "Bush_1_A_Color1", "Bush_1_C_Color1", "Bush_2_A_Color1", "Bush_3_A_Color1", "Bush_4_A_Color1" };
        static readonly string[] GRASSES = { "Grass_1_A_Color1", "Grass_1_C_Color1", "Grass_2_A_Color1", "Grass_2_C_Color1" };
        static readonly string[] ROCKS = { "Rock_1_A_Color1", "Rock_2_A_Color1", "Rock_3_A_Color1" };

        // ------------------------------------------------------------------ estado
        static Transform gChao, gCon, gProps, gNat, gLuz;
        static System.Action<Vector3, float> reserve;
        static System.Random rng = new System.Random(4242);
        static Texture2D texTile, texBrick, texPlank;

        /// <summary>Monta toda a praça em níveis. reserveFn = LevelBuilder.Reserve (evita árvores dentro das peças).</summary>
        public static void Build(Transform chao, Transform con, Transform props, Transform nat, Transform luz,
                                 System.Action<Vector3, float> reserveFn, float townHalf)
        {
            gChao = chao; gCon = con; gProps = props; gNat = nat; gLuz = luz;
            reserve = reserveFn;
            rng = new System.Random(4242);
            texTile = D.StoneTileTexture();
            texBrick = D.BrickTexture();
            texPlank = D.PlankTexture();

            LowerPlaza();
            FountainTerrace();
            Canal();
            MarketDeck();
            TavernDeck();
            GuildBalcony();
            Lookout();
            Ramparts(-townHalf);
            Hills();
        }

        // =================================================================================================
        //  utilidades
        // =================================================================================================
        static float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static float YawOf(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        static T Pick<T>(T[] a) => a[rng.Next(a.Length)];

        static Transform Root(string name, Transform parent)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        /// <summary>Reserva um retângulo (círculos em grade) para o ScatterTown não pôr árvores/moitas dentro.</summary>
        static void ReserveRect(float x0, float x1, float z0, float z1, float pad = 0.5f)
        {
            if (reserve == null) return;
            x0 -= pad; x1 += pad; z0 -= pad; z1 += pad;
            const float stepD = 2.5f;
            int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / stepD));
            int nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / stepD));
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= nz; j++)
                    reserve(new Vector3(Mathf.Lerp(x0, x1, i / (float)nx), 0f, Mathf.Lerp(z0, z1, j / (float)nz)), 1.8f);
        }

        /// <summary>Caixa texturizada pelos limites (sem rotação).</summary>
        static GameObject BoxMM(Transform p, float x0, float x1, float y0, float y1, float z0, float z1, Texture2D tex, float tile, Color tint, bool collider = false, string name = "Bloco")
        {
            var c = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            var s = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
            return D.TexturedBox(p, c, s, 0f, tex, tile, tint, collider, name);
        }

        /// <summary>
        /// Plataforma maciça: corpo (laterais) + tampo (piso) com uma borda opcional; UM colisor com topo
        /// exatamente em "top". Use lip = 0 em peças encostadas umas nas outras (evita z-fighting dos tampos).
        /// </summary>
        static Transform Platform(string name, float x0, float x1, float z0, float z1, float top,
                                  Texture2D sideTex, float sideTile, Color side, Texture2D capTex, float capTile, Color cap,
                                  float capH = 0.14f, float lip = 0.07f, float baseY = 0f)
        {
            var root = Root(name, gCon);
            BoxMM(root, x0, x1, baseY - 0.05f, top - capH, z0, z1, sideTex, sideTile, side, false, "Corpo");
            BoxMM(root, x0 - lip, x1 + lip, top - capH, top + 0.01f, z0 - lip, z1 + lip, capTex, capTile, cap, false, "Tampo");
            D.ColliderBox(root, new Vector3((x0 + x1) * 0.5f, (baseY + top) * 0.5f, (z0 + z1) * 0.5f),
                          new Vector3(x1 - x0, top - baseY, z1 - z0), 0f, "Colisor_Plataforma");
            return root;
        }

        /// <summary>Base ortonormal de um plano inclinado de p0 (baixo) a p1 (alto): f = ao longo, n = "cima" do plano.</summary>
        static bool SlopeFrame(Vector3 p0, Vector3 p1, out Vector3 f, out Vector3 n, out float len)
        {
            Vector3 d = p1 - p0;
            len = d.magnitude;
            f = Vector3.forward; n = Vector3.up;
            if (len < 0.01f) return false;
            f = d / len;
            Vector3 flat = U.Flat(f);
            if (flat.sqrMagnitude < 1e-4f) return false;
            Vector3 right = Vector3.Cross(Vector3.up, flat.normalized);
            n = Vector3.Cross(f, right).normalized;
            return true;
        }

        /// <summary>Colisor invisível cuja face de cima vai exatamente de p0 a p1.</summary>
        static void RampCollider(Transform parent, Vector3 p0, Vector3 p1, float width, float thick = 0.6f)
        {
            if (!SlopeFrame(p0, p1, out var f, out var n, out float len)) return;
            var g = new GameObject("Colisor_Rampa");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = (p0 + p1) * 0.5f - n * (thick * 0.5f);
            g.transform.localRotation = Quaternion.LookRotation(f, n);
            g.AddComponent<BoxCollider>().size = new Vector3(width, thick, len);
        }

        /// <summary>Colisor de parapeito/corrimão (1,6 m) entre dois pontos da base (pode ser inclinado).</summary>
        static void RailCollider(Transform parent, Vector3 a, Vector3 b)
        {
            if (!SlopeFrame(a, b, out var f, out var n, out float len)) return;
            var g = new GameObject("Colisor_Parapeito");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = (a + b) * 0.5f + Vector3.up * 0.7f;
            g.transform.localRotation = Quaternion.LookRotation(f, n);
            g.AddComponent<BoxCollider>().size = new Vector3(0.3f, 1.6f, len);
        }

        /// <summary>
        /// Escada: "start" = meio da borda do 1º degrau (y = piso de baixo); sobe na direção "dir".
        /// O último degrau fica no nível de cima. Degraus só visuais + rampa invisível + colisor do último degrau.
        /// rails: RAIL_L / RAIL_R (lados vistos de quem sobe).
        /// </summary>
        static Transform Stairs(string name, Vector3 start, Vector3 dir, float width, int steps, float rise, float run,
                                Texture2D tex, float tile, Color tint, int rails, bool stoneRails, Transform parent = null)
        {
            dir = U.Flat(dir).normalized;
            float yaw = YawOf(dir);
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            float y0 = start.y;
            var root = Root(name, parent != null ? parent : gCon);
            for (int i = 0; i < steps; i++)
            {
                float top = y0 + (i + 1) * rise;
                float bottom = y0 - 0.05f;
                Vector3 c = start + dir * ((i + 0.5f) * run);
                c.y = (top + bottom) * 0.5f;
                D.TexturedBox(root, c, new Vector3(width, top - bottom, run + 0.002f), yaw, tex, tile, tint, false, "Degrau");
            }
            float total = steps * rise;
            // rampa pelo meio dos espelhos: de meio degrau antes do 1º até o meio do último
            Vector3 p0 = start - dir * (run * 0.5f); p0.y = y0;
            Vector3 p1 = start + dir * ((steps - 0.5f) * run); p1.y = y0 + total;
            RampCollider(root, p0, p1, width);
            Vector3 lt = start + dir * ((steps - 0.5f) * run); lt.y = y0 + total - 0.15f;
            D.ColliderBox(root, lt, new Vector3(width, 0.3f, run), yaw, "Colisor_Degrau");
            Vector3 b = start + dir * (steps * run); b.y = y0 + total;
            float off = width * 0.5f - 0.14f;
            if ((rails & RAIL_L) != 0) Rail(root, start - right * off, b - right * off, stoneRails);
            if ((rails & RAIL_R) != 0) Rail(root, start + right * off, b + right * off, stoneRails);
            return root;
        }

        /// <summary>Parapeito de pedra (degraus se inclinado) ou corrimão de madeira de a até b (pontos da base) + colisor.</summary>
        static void Rail(Transform parent, Vector3 a, Vector3 b, bool stone, float h = 0.9f)
        {
            Vector3 d = b - a;
            Vector3 flat = U.Flat(d);
            float flatLen = flat.magnitude;
            if (flatLen < 0.05f) return;
            float len = d.magnitude;
            float yaw = YawOf(flat);
            bool sloped = Mathf.Abs(d.y) > 0.02f;
            if (stone)
            {
                int segs = sloped ? Mathf.Max(1, Mathf.CeilToInt(flatLen / 0.55f)) : 1;
                float segLen = flatLen / segs;
                float extra = sloped ? 0.3f : 0.05f;   // inclinado: desce um pouco para fechar a fresta sobre os degraus
                for (int i = 0; i < segs; i++)
                {
                    Vector3 c = a + d * ((i + 0.5f) / segs);
                    D.TexturedBox(parent, new Vector3(c.x, c.y + (h - extra) * 0.5f, c.z), new Vector3(0.34f, h + extra, segLen + (sloped ? 0.01f : 0f)),
                                  yaw, texBrick, 2f, STONE_WALL, false, "Parapeito");
                }
                if (!sloped)
                    D.TexturedBox(parent, (a + b) * 0.5f + Vector3.up * (h + 0.05f), new Vector3(0.44f, 0.1f, flatLen + 0.1f), yaw, texTile, 3f, CAP_STONE, false, "Capa");
            }
            else
            {
                SlopeFrame(a, b, out var f, out var n, out _);
                Quaternion rot = Quaternion.LookRotation(f, n);
                int posts = Mathf.Max(2, Mathf.CeilToInt(flatLen / 1.4f) + 1);
                for (int i = 0; i < posts; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, i / (float)(posts - 1));
                    D.TexturedBox(parent, p + Vector3.up * (h * 0.5f - 0.05f), new Vector3(0.13f, h + 0.1f, 0.13f), yaw, texPlank, 1.6f, WOOD_DARK, false, "Poste");
                }
                D.TexturedBoxRot(parent, (a + b) * 0.5f + Vector3.up * h, new Vector3(0.12f, 0.09f, len + 0.1f), rot, texPlank, 1.6f, WOOD, false, "Corrimao");
                D.TexturedBoxRot(parent, (a + b) * 0.5f + Vector3.up * (h * 0.5f), new Vector3(0.06f, 0.07f, len), rot, texPlank, 1.6f, WOOD, false, "Travessa");
            }
            RailCollider(parent, a, b);
        }

        /// <summary>
        /// Rampa lisa de p0 (baixo) a p1 (alto): a laje visível É o colisor. stoneSides = muretas laterais em degraus
        /// (escondem o vão por baixo) com colisor; woodRails = corrimãos de madeira dos dois lados.
        /// </summary>
        static Transform Ramp(string name, Vector3 p0, Vector3 p1, float width, Texture2D tex, float tile, Color tint, bool stoneSides, bool woodRails = false)
        {
            var root = Root(name, gCon);
            if (!SlopeFrame(p0, p1, out var f, out var n, out float len)) return root;
            const float thick = 0.45f;
            D.TexturedBoxRot(root, (p0 + p1) * 0.5f - n * (thick * 0.5f), new Vector3(width, thick, len), Quaternion.LookRotation(f, n), tex, tile, tint, true, "Rampa");
            Vector3 flat = U.Flat(p1 - p0);
            float flatLen = flat.magnitude;
            Vector3 dir = flat / flatLen;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            float yaw = YawOf(dir);
            if (stoneSides)
            {
                int segs = Mathf.Max(1, Mathf.CeilToInt(flatLen / 0.5f));
                float segLen = flatLen / segs;
                float botY = Mathf.Min(p0.y, p1.y) - 0.05f;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 off = right * (s * (width * 0.5f + 0.2f));
                    for (int i = 0; i < segs; i++)
                    {
                        Vector3 c = Vector3.Lerp(p0, p1, (i + 0.5f) / segs) + off;
                        float topY = c.y + 0.55f;
                        D.TexturedBox(root, new Vector3(c.x, (topY + botY) * 0.5f, c.z), new Vector3(0.4f, topY - botY, segLen + 0.01f), yaw, texBrick, 2f, STONE_WALL, false, "Mureta");
                    }
                    RailCollider(root, p0 + off, p1 + off);
                }
            }
            else if (woodRails)
            {
                float off = width * 0.5f - 0.1f;
                Rail(root, p0 + right * off, p1 + right * off, false);
                Rail(root, p0 - right * off, p1 - right * off, false);
            }
            return root;
        }

        // ------------------------------------------------------------------ peças de decoração
        static void Planter(Vector3 p, float sx, float sz, float h = 0.55f)
        {
            var root = Root("Canteiro", gProps);
            BoxMM(root, p.x - sx * 0.5f, p.x + sx * 0.5f, p.y - 0.02f, p.y + h, p.z - sz * 0.5f, p.z + sz * 0.5f, texBrick, 1.2f, STONE_WALL, true, "Mureta");
            D.Slab(root, new Vector3(p.x, p.y + h + 0.005f, p.z), new Vector3(sx - 0.24f, 0.02f, sz - 0.24f), DIRT);
            int bushes = sx * sz > 2.6f ? 2 : 1;
            for (int i = 0; i < bushes; i++)
                D.SpawnSized(Pick(BUSHES), gNat, new Vector3(p.x + Rf(-0.2f, 0.2f) * sx, p.y + h, p.z + Rf(-0.2f, 0.2f) * sz), Rf(0, 360), Rf(0.6f, 0.9f));
            int flowers = Mathf.Clamp(Mathf.RoundToInt(sx * sz * 2.5f), 4, 9);
            for (int i = 0; i < flowers; i++)
            {
                Vector3 q = new Vector3(p.x + Rf(-0.5f, 0.5f) * (sx - 0.4f), p.y + h + 0.1f, p.z + Rf(-0.5f, 0.5f) * (sz - 0.4f));
                U.Prim(PrimitiveType.Sphere, root, q, Vector3.one * Rf(0.13f, 0.2f), Pick(FLOWERS));
            }
        }

        /// <summary>Banco de madeira com pés de pedra. face = para onde a pessoa sentada olha.</summary>
        static void Bench(Vector3 p, Vector3 face)
        {
            face = U.Flat(face).normalized;
            float yaw = YawOf(face);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            var root = Root("Banco", gProps);
            D.TexturedBox(root, p + Vector3.up * 0.44f, new Vector3(1.8f, 0.09f, 0.5f), yaw, texPlank, 1.6f, WOOD, false, "Assento");
            D.TexturedBox(root, p + q * new Vector3(0f, 0.78f, -0.24f), new Vector3(1.8f, 0.36f, 0.07f), yaw, texPlank, 1.6f, WOOD, false, "Encosto");
            for (int s = -1; s <= 1; s += 2)
                D.TexturedBox(root, p + q * new Vector3(0.68f * s, 0.2f, 0f), new Vector3(0.22f, 0.42f, 0.46f), yaw, texBrick, 1.2f, STONE_GREY, false, "Pe");
            D.ColliderBox(root, p + q * new Vector3(0f, 0.5f, -0.02f), new Vector3(1.8f, 1.0f, 0.55f), yaw, "Colisor_Banco");
        }

        /// <summary>Pilar de pedra com lanterna (brilho emissivo; luz real só se "light").</summary>
        static void LanternPillar(Vector3 p, float h, bool light, float range = 7f, float intensity = 1.6f)
        {
            var root = Root("Pilar_Lanterna", gProps);
            BoxMM(root, p.x - 0.32f, p.x + 0.32f, p.y - 0.02f, p.y + h, p.z - 0.32f, p.z + 0.32f, texBrick, 1.2f, STONE_WALL, true, "Pilar");
            BoxMM(root, p.x - 0.4f, p.x + 0.4f, p.y + h, p.y + h + 0.12f, p.z - 0.4f, p.z + 0.4f, texTile, 2f, CAP_STONE, false, "Capitel");
            D.SpawnSized("lantern", root, new Vector3(p.x, p.y + h + 0.12f, p.z), 0f, 0.55f);
            var glow = U.Prim(PrimitiveType.Sphere, root, new Vector3(p.x, p.y + h + 0.38f, p.z), Vector3.one * 0.2f, LANTERN_GLOW);
            glow.GetComponent<Renderer>().sharedMaterial = U.Lit(LANTERN_GLOW, 0.2f, LANTERN_GLOW * 1.6f);
            if (light) D.PointLight(gLuz, new Vector3(p.x, p.y + h + 0.6f, p.z), D.TorchColor, range, intensity, false, true);
        }

        /// <summary>Estandarte pendurado numa parede. top = meio da borda de cima; outward = normal da parede.</summary>
        static void Banner(Vector3 top, Vector3 outward, float w, float h, Color cloth, Color trim)
        {
            outward = U.Flat(outward).normalized;
            float yaw = YawOf(outward);
            var root = Root("Estandarte", gProps);
            D.Slab(root, top + outward * 0.05f - Vector3.up * (h * 0.5f), new Vector3(w, h, 0.04f), cloth, yaw);
            D.Slab(root, top + outward * 0.09f, new Vector3(w + 0.25f, 0.08f, 0.08f), WOOD_DARK, yaw);
            D.Slab(root, top + outward * 0.075f - Vector3.up * (h - 0.12f), new Vector3(w, 0.12f, 0.03f), trim, yaw);
            D.Slab(root, top + outward * 0.075f - Vector3.up * (h * 0.45f), new Vector3(w * 0.42f, w * 0.42f, 0.03f), trim, yaw);
        }

        /// <summary>Hera pendurada a partir da borda de cima de uma parede.</summary>
        static void Ivy(Vector3 top, Vector3 outward, float w, float h)
        {
            outward = U.Flat(outward).normalized;
            float yaw = YawOf(outward);
            Vector3 right = Vector3.Cross(Vector3.up, outward);
            int n = Mathf.Max(2, Mathf.RoundToInt(w / 0.4f));
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n - 0.5f;
                float hh = h * Rf(0.4f, 1f);
                Vector3 c = top + right * (t * w) + outward * Rf(0.02f, 0.06f) - Vector3.up * (hh * 0.5f);
                D.Slab(gNat, c, new Vector3(Rf(0.3f, 0.55f), hh, 0.04f), rng.NextDouble() < 0.5 ? IVY : IVY_DARK, yaw);
            }
        }

        /// <summary>Barraca de feira: balcão, 4 postes e toldo listrado inclinado. face = lado do freguês.</summary>
        static void Stall(Vector3 p, Vector3 face, Color c1, Color c2)
        {
            face = U.Flat(face).normalized;
            float yaw = YawOf(face);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            var root = Root("Barraca", gProps);
            D.TexturedBox(root, p + q * new Vector3(0f, 0.45f, 0.35f), new Vector3(2.2f, 0.9f, 0.7f), yaw, texPlank, 1.6f, WOOD, false, "Balcao");
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    float ph = sz > 0 ? 2.1f : 2.5f;
                    D.TexturedBox(root, p + q * new Vector3(1.05f * sx, ph * 0.5f, 0.35f + 0.55f * sz), new Vector3(0.1f, ph, 0.1f), yaw, texPlank, 1.6f, WOOD_DARK, false, "Poste");
                }
            Quaternion tilt = q * Quaternion.Euler(20f, 0f, 0f);   // desce para a frente
            for (int i = 0; i < 5; i++)
            {
                float x = -1.0f + i * 0.5f;
                D.TexturedBoxRot(root, p + q * new Vector3(x, 2.33f, 0.35f), new Vector3(0.5f, 0.05f, 1.35f), tilt, null, 1f, i % 2 == 0 ? c1 : c2, false, "Toldo");
            }
            D.SpawnSized("crate_B_small", root, p + q * new Vector3(0.6f, 0.9f, 0.3f), yaw + 15f, 0.35f);
            D.SpawnSized("sack", root, p + q * new Vector3(-0.7f, 0.9f, 0.35f), yaw, 0.3f);
            Color[] fruit = { new Color(0.85f, 0.18f, 0.15f), new Color(1f, 0.6f, 0.15f), new Color(0.5f, 0.75f, 0.25f) };
            for (int i = 0; i < 5; i++)
                U.Prim(PrimitiveType.Sphere, root, p + q * new Vector3(-0.1f + i * 0.13f, 0.97f, 0.42f + (i % 2) * 0.1f), Vector3.one * 0.14f, fruit[i % fruit.Length]);
            D.ColliderBox(root, p + q * new Vector3(0f, 0.6f, 0.35f), new Vector3(2.3f, 1.2f, 0.8f), yaw, "Colisor_Barraca");
        }

        static GameObject PropC(string id, Vector3 pos, float yaw, float height, Transform parent = null)
        {
            var g = D.SpawnSized(id, parent != null ? parent : gProps, pos, yaw, height);
            D.AddCollider(g, 0.85f, 1.2f);
            return g;
        }

        static void Lamp(Vector3 p, float intensity = 1.6f, float range = 7f)
        {
            D.LampPost(gProps, gLuz, p, intensity, range);
            D.ColliderBox(gProps, p + new Vector3(0, 1.4f, 0), new Vector3(0.4f, 2.8f, 0.4f), 0f, "Colisor_Poste");
        }

        // =================================================================================================
        //  PRAÇA BAIXA (y 0): x -12,5..12,5 · z -16,5..0
        // =================================================================================================
        static void LowerPlaza()
        {
            D.TiledSlab(gChao, new Vector3(0f, 0.03f - 0.1f, -8.25f), new Vector3(25f, 0.2f, 16.5f), texTile, 7f, TILE_PLAZA);
            D.Slab(gChao, new Vector3(0f, 0.012f, -8.25f), new Vector3(26.4f, 0.02f, 17.9f), DIRT);
            // rosa-dos-ventos no centro
            D.Disc(gChao, new Vector3(0f, 0.04f, -8.5f), 2.8f, TILE_SLATE);
            D.Disc(gChao, new Vector3(0f, 0.045f, -8.5f), 2.5f, TILE_ROSE);
            D.Slab(gChao, new Vector3(0f, 0.058f, -8.5f), new Vector3(0.6f, 0.01f, 4.6f), CAP_STONE, 0f);
            D.Slab(gChao, new Vector3(0f, 0.059f, -8.5f), new Vector3(0.6f, 0.01f, 4.6f), CAP_STONE, 90f);
            D.Slab(gChao, new Vector3(0f, 0.06f, -8.5f), new Vector3(0.4f, 0.01f, 3.2f), TILE_SLATE, 45f);
            D.Slab(gChao, new Vector3(0f, 0.061f, -8.5f), new Vector3(0.4f, 0.01f, 3.2f), TILE_SLATE, -45f);
            D.Disc(gChao, new Vector3(0f, 0.066f, -8.5f), 0.7f, CAP_STONE);
            // canteiros e bancos nas bordas
            Planter(new Vector3(-10.6f, 0f, -14.6f), 1.8f, 1.8f);
            Planter(new Vector3(10.6f, 0f, -14.6f), 1.8f, 1.8f);
            Planter(new Vector3(10.4f, 0f, -4.4f), 1.6f, 1.6f);
            Bench(new Vector3(-5.5f, 0f, -15.6f), Vector3.forward);
            Bench(new Vector3(5.5f, 0f, -15.6f), Vector3.forward);
            Bench(new Vector3(11.4f, 0f, -8f), Vector3.left);
            ReserveRect(-13f, 13f, -17f, 0f);
        }

        // =================================================================================================
        //  TERRAÇO DA FONTE (y 1,5): x -8..8 · z 0..12
        // =================================================================================================
        static void FountainTerrace()
        {
            const float T = TERRACE_TOP;
            Platform("Terraco_Fonte", -8f, 8f, 0f, 12f, T, texBrick, 2f, STONE_WALL, texTile, 4.5f, TILE_TERRACE, 0.16f, 0.08f);
            // mosaico em volta da fonte
            D.Disc(gChao, new Vector3(0f, T + 0.016f, FOUNTAIN.z), 3.7f, TILE_SLATE);
            D.Disc(gChao, new Vector3(0f, T + 0.02f, FOUNTAIN.z), 3.4f, TILE_ROSE);
            Fountain(FOUNTAIN);

            // acessos: escadaria larga (sul), escada da Torre (norte), escada leste, rampa oeste
            Stairs("Escadaria_Sul", new Vector3(0f, 0f, -3f), Vector3.forward, 8f, 6, 0.25f, 0.5f, texTile, 2.5f, STEP_STONE, RAIL_BOTH, true);
            Stairs("Escada_Norte", new Vector3(0f, 0f, 15f), Vector3.back, 4f, 6, 0.25f, 0.5f, texTile, 2.5f, STEP_STONE, RAIL_BOTH, true);
            Stairs("Escada_Leste", new Vector3(11f, 0f, 4.5f), Vector3.left, 3f, 6, 0.25f, 0.5f, texTile, 2.5f, STEP_STONE, RAIL_BOTH, true);
            Ramp("Rampa_Oeste", new Vector3(-12f, 0f, 8.5f), new Vector3(-8f, T, 8.5f), 3f, texTile, 2.5f, STEP_STONE, true);

            // parapeitos nas bordas (com aberturas nos acessos)
            var par = Root("Terraco_Parapeitos", gCon);
            const float e = 0.2f, ph = 0.7f;
            Rail(par, new Vector3(-8f + e, T, e), new Vector3(-4f, T, e), true, ph);
            Rail(par, new Vector3(4f, T, e), new Vector3(8f - e, T, e), true, ph);
            Rail(par, new Vector3(-8f + e, T, 12f - e), new Vector3(-2f, T, 12f - e), true, ph);
            Rail(par, new Vector3(2f, T, 12f - e), new Vector3(8f - e, T, 12f - e), true, ph);
            Rail(par, new Vector3(8f - e, T, e), new Vector3(8f - e, T, 3f), true, ph);
            Rail(par, new Vector3(8f - e, T, 6f), new Vector3(8f - e, T, 12f - e), true, ph);
            Rail(par, new Vector3(-8f + e, T, e), new Vector3(-8f + e, T, 7f - 0.4f), true, ph);
            Rail(par, new Vector3(-8f + e, T, 10f + 0.4f), new Vector3(-8f + e, T, 12f - e), true, ph);

            // lanternas: cantos do terraço e pé da escadaria
            LanternPillar(new Vector3(-7.75f, T, 11.75f), 1.25f, true);
            LanternPillar(new Vector3(7.75f, T, 11.75f), 1.25f, false);
            LanternPillar(new Vector3(-7.75f, T, 0.25f), 1.25f, false);
            LanternPillar(new Vector3(7.75f, T, 0.25f), 1.25f, true);
            LanternPillar(new Vector3(-4.35f, 0f, -3.3f), 1.4f, true);
            LanternPillar(new Vector3(4.35f, 0f, -3.3f), 1.4f, true);

            // canteiros e bancos em volta da fonte
            Planter(new Vector3(-5.6f, T, 10.4f), 2.2f, 1.4f);
            Planter(new Vector3(5.6f, T, 10.4f), 2.2f, 1.4f);
            Planter(new Vector3(-6.2f, T, 1.6f), 1.5f, 1.5f);
            Planter(new Vector3(6.2f, T, 1.6f), 1.5f, 1.5f);
            Bench(new Vector3(-4.9f, T, FOUNTAIN.z), Vector3.right);
            Bench(new Vector3(4.9f, T, FOUNTAIN.z), Vector3.left);

            // estandartes e hera nas faces sul/oeste (as que a câmera vê)
            Banner(new Vector3(-6f, T - 0.05f, -0.1f), Vector3.back, 1.1f, 1.25f, new Color(0.18f, 0.31f, 0.56f), new Color(0.88f, 0.72f, 0.29f));
            Banner(new Vector3(6f, T - 0.05f, -0.1f), Vector3.back, 1.1f, 1.25f, new Color(0.56f, 0.18f, 0.23f), new Color(0.88f, 0.72f, 0.29f));
            Banner(new Vector3(-8.1f, T - 0.05f, 3.6f), Vector3.left, 1.0f, 1.2f, new Color(0.18f, 0.31f, 0.56f), new Color(0.88f, 0.72f, 0.29f));
            Ivy(new Vector3(-8.06f, T, 1.4f), Vector3.left, 1.8f, 1.3f);
            Ivy(new Vector3(-7.2f, T, -0.06f), Vector3.back, 1.2f, 1.2f);
            Ivy(new Vector3(7.0f, T, -0.06f), Vector3.back, 1.0f, 1.1f);

            ReserveRect(-12.5f, 12f, -3.6f, 15.5f);
        }

        static void Fountain(Vector3 c)
        {
            var root = Root("Fonte", gProps);
            float y = c.y;
            // bacia (colisor de malha: o cilindro achatado viraria esfera com CapsuleCollider)
            var basin = U.Prim(PrimitiveType.Cylinder, root, new Vector3(c.x, y + 0.3f, c.z), new Vector3(4.6f, 0.3f, 4.6f), STONE_WALL);
            basin.name = "Bacia";
            basin.AddComponent<MeshCollider>().sharedMesh = basin.GetComponent<MeshFilter>().sharedMesh;
            U.Prim(PrimitiveType.Cylinder, root, new Vector3(c.x, y + 0.53f, c.z), new Vector3(4.85f, 0.04f, 4.85f), CAP_STONE);   // friso
            var water = U.Prim(PrimitiveType.Cylinder, root, new Vector3(c.x, y + 0.61f, c.z), new Vector3(4.15f, 0.01f, 4.15f), WATER);
            water.GetComponent<Renderer>().sharedMaterial = U.Lit(WATER, 0.92f, WATER_GLOW);
            // coluna, taça de cima e remate
            U.Prim(PrimitiveType.Cylinder, root, new Vector3(c.x, y + 1.35f, c.z), new Vector3(0.7f, 0.75f, 0.7f), CAP_STONE);
            U.Prim(PrimitiveType.Cylinder, root, new Vector3(c.x, y + 2.2f, c.z), new Vector3(1.9f, 0.12f, 1.9f), STONE_WALL);
            var w2 = U.Prim(PrimitiveType.Cylinder, root, new Vector3(c.x, y + 2.33f, c.z), new Vector3(1.6f, 0.01f, 1.6f), WATER);
            w2.GetComponent<Renderer>().sharedMaterial = U.Lit(WATER, 0.92f, WATER_GLOW);
            U.Prim(PrimitiveType.Sphere, root, new Vector3(c.x, y + 2.55f, c.z), Vector3.one * 0.38f, CAP_STONE);
            // água: jato central e respingos caindo da taça para a bacia
            Color spray = new Color(0.75f, 0.92f, 1f, 0.75f);
            D.WaterSpray(root, new Vector3(c.x, y + 2.7f, c.z), 0.9f, spray, 40, 6f, 0.3f);
            D.WaterSpray(root, new Vector3(c.x, y + 2.35f, c.z), 0.12f, spray, 36, 55f, 1.7f);
            D.Motes(gProps, new Vector3(c.x, y + 0.9f, c.z), new Vector3(3.6f, 0.6f, 3.6f), new Color(0.7f, 0.92f, 1f, 0.8f), 14, 0.02f);
            D.PointLight(gLuz, new Vector3(c.x, y + 1.6f, c.z), new Color(0.65f, 0.85f, 1f), 6f, 0.9f, false, false);
        }

        // =================================================================================================
        //  CANAL (bordas elevadas, x 13,3..16,7 · z -9..7) + PONTE no caminho da Guilda
        // =================================================================================================
        static void Canal()
        {
            const float x0 = 13.3f, x1 = 16.7f, z0 = -9f, z1 = 7f, wt = 0.5f, wh = 0.6f;
            var root = Root("Canal", gCon);
            BoxMM(root, x0, x0 + wt, -0.05f, wh, z0, z1, texBrick, 1.5f, STONE_WALL, true, "Margem_O");
            BoxMM(root, x1 - wt, x1, -0.05f, wh, z0, z1, texBrick, 1.5f, STONE_WALL, true, "Margem_L");
            BoxMM(root, x0 + wt, x1 - wt, -0.05f, wh, z0, z0 + wt, texBrick, 1.5f, STONE_WALL, true, "Margem_S");
            BoxMM(root, x0 + wt, x1 - wt, -0.05f, wh, z1 - wt, z1, texBrick, 1.5f, STONE_WALL, true, "Margem_N");
            BoxMM(root, x0 - 0.06f, x0 + wt + 0.06f, wh, wh + 0.08f, z0 - 0.06f, z1 + 0.06f, texTile, 2f, CAP_STONE, false, "Capa_O");
            BoxMM(root, x1 - wt - 0.06f, x1 + 0.06f, wh, wh + 0.08f, z0 - 0.06f, z1 + 0.06f, texTile, 2f, CAP_STONE, false, "Capa_L");
            BoxMM(root, x0 + wt + 0.06f, x1 - wt - 0.06f, wh, wh + 0.08f, z0 - 0.06f, z0 + wt + 0.06f, texTile, 2f, CAP_STONE, false, "Capa_S");
            BoxMM(root, x0 + wt + 0.06f, x1 - wt - 0.06f, wh, wh + 0.08f, z1 - wt - 0.06f, z1 + 0.06f, texTile, 2f, CAP_STONE, false, "Capa_N");
            var water = D.Slab(root, new Vector3((x0 + x1) * 0.5f, 0.4f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 - 2f * wt + 0.02f, 0.04f, z1 - z0 - 2f * wt + 0.02f), WATER);
            water.name = "Agua";
            water.GetComponent<Renderer>().sharedMaterial = U.Lit(WATER, 0.92f, WATER_GLOW);
            // bica na ponta norte com um filete de água caindo
            BoxMM(root, 14.7f, 15.3f, wh, wh + 0.55f, z1 - 0.45f, z1 + 0.05f, texBrick, 1.2f, STONE_GREY, false, "Bica");
            var fall = D.Slab(root, new Vector3(15f, 0.62f, z1 - 0.52f), new Vector3(0.26f, 0.42f, 0.05f), WATER);
            fall.GetComponent<Renderer>().sharedMaterial = U.Lit(new Color(0.55f, 0.8f, 0.9f), 0.9f, WATER_GLOW * 3f);
            D.Motes(gProps, new Vector3(15f, 0.5f, z1 - 0.9f), new Vector3(0.7f, 0.2f, 0.5f), new Color(0.85f, 0.97f, 1f, 0.9f), 10, 0.1f);
            D.Motes(gNat, new Vector3(15f, 0.55f, (z0 + z1) * 0.5f), new Vector3(2.2f, 0.25f, 14f), new Color(0.75f, 0.95f, 1f, 0.8f), 16, 0f);
            Ivy(new Vector3(x0, wh, -5f), Vector3.left, 1.4f, 0.5f);
            ReserveRect(x0, x1, z0, z1);

            // ---- ponte (rampas lisas + vão de madeira sobre as margens)
            const float y = BRIDGE_TOP, w = 3.4f;
            var br = Root("Ponte", gCon);
            BoxMM(br, x0, x1, wh + 0.02f, y, -w * 0.5f, w * 0.5f, texPlank, 1.6f, WOOD, true, "Tabuleiro");
            Ramp("Ponte_Rampa_O", new Vector3(11.8f, 0f, 0f), new Vector3(x0, y, 0f), w, texPlank, 1.6f, WOOD, true);
            Ramp("Ponte_Rampa_L", new Vector3(18.2f, 0f, 0f), new Vector3(x1, y, 0f), w, texPlank, 1.6f, WOOD, true);
            Rail(br, new Vector3(x0, y, w * 0.5f + 0.2f), new Vector3(x1, y, w * 0.5f + 0.2f), true, 0.55f);
            Rail(br, new Vector3(x0, y, -w * 0.5f - 0.2f), new Vector3(x1, y, -w * 0.5f - 0.2f), true, 0.55f);
            LanternPillar(new Vector3(11.55f, 0f, 2.45f), 1.2f, true);
            LanternPillar(new Vector3(11.55f, 0f, -2.45f), 1.2f, false);
            LanternPillar(new Vector3(18.45f, 0f, 2.45f), 1.2f, false);
            LanternPillar(new Vector3(18.45f, 0f, -2.45f), 1.2f, true);
            ReserveRect(11.5f, 18.5f, -2.6f, 2.6f);
        }

        // =================================================================================================
        //  DECK DO MERCADO (y 0,75): x -21,5..-13 · z -15..-8
        // =================================================================================================
        static void MarketDeck()
        {
            const float T = MARKET_TOP;
            const float x0 = -21.5f, x1 = -13f, z0 = -15f, z1 = -8f;
            Platform("Deck_Mercado", x0, x1, z0, z1, T, texPlank, 1.6f, WOOD_DARK, texPlank, 1.6f, WOOD, 0.12f, 0.06f);
            for (float x = x0 + 0.5f; x < x1; x += 2.1f)
                BoxMM(gCon, x - 0.1f, x + 0.1f, -0.02f, T - 0.1f, z0 - 0.09f, z0 + 0.02f, texPlank, 1.6f, BEAM, false, "Viga");
            for (float z = z0 + 0.5f; z < z1; z += 2.1f)
                BoxMM(gCon, x1 - 0.02f, x1 + 0.09f, -0.02f, T - 0.1f, z - 0.1f, z + 0.1f, texPlank, 1.6f, BEAM, false, "Viga");
            Stairs("Deck_Mercado_Degraus", new Vector3(-11.5f, 0f, -11.5f), Vector3.left, 2.4f, 3, 0.25f, 0.5f, texPlank, 1.6f, WOOD, RAIL_BOTH, false);
            Ramp("Deck_Mercado_Rampa", new Vector3(-17.8f, 0f, -6f), new Vector3(-17.8f, T, z1), 2.2f, texPlank, 1.6f, WOOD, false, true);
            var rr = Root("Deck_Mercado_Corrimaos", gCon);
            const float e = 0.12f;
            Rail(rr, new Vector3(x0 + e, T, z0 + e), new Vector3(x1 - e, T, z0 + e), false);
            Rail(rr, new Vector3(x0 + e, T, z0 + e), new Vector3(x0 + e, T, z1 - e), false);
            Rail(rr, new Vector3(x1 - e, T, z0 + e), new Vector3(x1 - e, T, -12.7f), false);
            Rail(rr, new Vector3(x1 - e, T, -10.3f), new Vector3(x1 - e, T, z1 - e), false);
            Rail(rr, new Vector3(x0 + e, T, z1 - e), new Vector3(-18.9f, T, z1 - e), false);
            Rail(rr, new Vector3(-16.7f, T, z1 - e), new Vector3(x1 - e, T, z1 - e), false);
            // tendas, barraca e mercadorias
            PropC("tent", new Vector3(-19.6f, T, -13.1f), 45f, LevelBuilder.H_TENT);
            PropC("tent", new Vector3(-15.2f, T, -13.3f), 20f, LevelBuilder.H_TENT);
            Stall(new Vector3(-20.1f, T, -9.6f), Vector3.right, new Color(0.85f, 0.25f, 0.2f), new Color(0.95f, 0.9f, 0.8f));
            PropC("crate_A_big", new Vector3(-21.0f, T, -14.4f), 15f, LevelBuilder.H_CRATE);
            D.SpawnSized("crate_B_small", gProps, new Vector3(-21.0f, T + LevelBuilder.H_CRATE, -14.4f), 40f, LevelBuilder.H_CRATE_S);
            PropC("crate_long_A", new Vector3(-17.4f, T, -14.5f), 80f, 0.7f);
            D.SpawnSized("sack", gProps, new Vector3(-14.0f, T, -14.3f), 0f, LevelBuilder.H_SACK);
            D.SpawnSized("sack", gProps, new Vector3(-14.5f, T, -14.5f), 60f, LevelBuilder.H_SACK);
            PropC("barrel", new Vector3(-13.7f, T, -8.8f), 0f, LevelBuilder.H_BARREL);
            Lamp(new Vector3(-21.0f, T, -8.6f), 1.4f, 6.5f);
            Lamp(new Vector3(-13.6f, T, -14.4f), 1.4f, 6.5f);
            ReserveRect(x0, -11.4f, z0, -5.8f);
        }

        // =================================================================================================
        //  DECK DA TAVERNA (y 1,0): x 15..19,4 · z 7,6..12,4 — o bardo toca aqui
        // =================================================================================================
        static void TavernDeck()
        {
            const float T = TAVERN_TOP;
            const float x0 = 15f, x1 = 19.4f, z0 = 7.6f, z1 = 12.4f;
            Platform("Deck_Taverna", x0, x1, z0, z1, T, texPlank, 1.6f, WOOD_DARK, texPlank, 1.6f, WOOD, 0.12f, 0.06f);
            for (float x = x0 + 0.4f; x < x1; x += 1.9f)
                BoxMM(gCon, x - 0.1f, x + 0.1f, -0.02f, T - 0.1f, z0 - 0.09f, z0 + 0.02f, texPlank, 1.6f, BEAM, false, "Viga");
            Stairs("Deck_Taverna_Degraus", new Vector3(13.2f, 0f, 9.8f), Vector3.right, 2.4f, 4, 0.25f, 0.45f, texPlank, 1.6f, WOOD, RAIL_BOTH, false);
            var rr = Root("Deck_Taverna_Corrimaos", gCon);
            const float e = 0.12f;
            Rail(rr, new Vector3(x0 + e, T, z0 + e), new Vector3(x1 - e, T, z0 + e), false);
            Rail(rr, new Vector3(x0 + e, T, z1 - e), new Vector3(x1 - e, T, z1 - e), false);
            Rail(rr, new Vector3(x1 - e, T, z0 + e), new Vector3(x1 - e, T, z1 - e), false);
            Rail(rr, new Vector3(x0 + e, T, z0 + e), new Vector3(x0 + e, T, 8.6f), false);
            Rail(rr, new Vector3(x0 + e, T, 11.0f), new Vector3(x0 + e, T, z1 - e), false);
            // mesa com barris de banco, barris e lanterna
            var table = D.TexturedBox(gProps, new Vector3(18.1f, T + 0.4f, 8.7f), new Vector3(1.5f, 0.8f, 0.9f), 0f, texPlank, 1.6f, WOOD, true, "Mesa");
            table.name = "Mesa";
            D.SpawnSized("bucket_metal", gProps, new Vector3(18.3f, T + 0.8f, 8.7f), 0f, 0.22f);
            D.SpawnSized("crate_B_small", gProps, new Vector3(17.8f, T + 0.8f, 8.6f), 25f, 0.2f);
            PropC("barrel", new Vector3(17.0f, T, 8.7f), 0f, 0.6f);
            PropC("barrel", new Vector3(18.7f, T, 11.7f), 0f, LevelBuilder.H_BARREL);
            PropC("barrel", new Vector3(15.8f, T, 11.8f), 30f, LevelBuilder.H_BARREL);
            Lamp(new Vector3(19.0f, T, 12.0f), 1.5f, 6.5f);
            ReserveRect(13f, x1, z0, z1);
        }

        // =================================================================================================
        //  SACADA DA GUILDA (y 2,0): x 17,8..21,8 · z -10..-5,8 — loja da Armeira com vista para a ponte
        // =================================================================================================
        static void GuildBalcony()
        {
            const float T = BALCONY_TOP;
            const float x0 = 17.8f, x1 = 21.8f, z0 = -10f, z1 = -5.8f;
            var b = Platform("Sacada_Guilda", x0, x1, z0, z1, T, texBrick, 2f, STONE_WALL, texTile, 3f, TILE_ROSE, 0.16f, 0.08f);
            D.MarkOccluder(b.gameObject);
            Stairs("Sacada_Guilda_Escada", new Vector3(19.8f, 0f, -2.2f), Vector3.back, 2f, 8, 0.25f, 0.45f, texTile, 2.5f, STEP_STONE, RAIL_BOTH, true);
            var rr = Root("Sacada_Guilda_Parapeitos", gCon);
            const float e = 0.2f, ph = 0.8f;
            Rail(rr, new Vector3(x0 + e, T, z1 - e), new Vector3(18.8f, T, z1 - e), true, ph);
            Rail(rr, new Vector3(20.8f, T, z1 - e), new Vector3(x1 - e, T, z1 - e), true, ph);
            Rail(rr, new Vector3(x1 - e, T, z1 - e), new Vector3(x1 - e, T, z0 + e), true, ph);
            Rail(rr, new Vector3(x0 + e, T, z0 + e), new Vector3(x1 - e, T, z0 + e), true, ph);
            Rail(rr, new Vector3(x0 + e, T, z0 + e), new Vector3(x0 + e, T, z1 - e), true, ph);
            var rack = D.SpawnSized("weaponrack", gProps, new Vector3(21.1f, T, -9.0f), -90f, LevelBuilder.H_RACK);
            D.AddCollider(rack, 0.85f, 1.2f);
            PropC("flag_red", new Vector3(21.2f, T, -6.4f), -90f, LevelBuilder.H_FLAG);
            D.StandingTorch(gProps, gLuz, new Vector3(18.4f, T, -9.4f), 2.4f, 7f);
            Banner(new Vector3(x0 - 0.08f, T - 0.05f, -7.9f), Vector3.left, 1.0f, 1.5f, new Color(0.56f, 0.18f, 0.23f), new Color(0.88f, 0.72f, 0.29f));
            Ivy(new Vector3(20.4f, T, z0 - 0.06f), Vector3.back, 1.6f, 1.4f);
            ReserveRect(x0, x1, z0, -2f);
        }

        // =================================================================================================
        //  MIRANTE (Torre de Vigia): núcleo maciço + 3 lances em caracol quadrado (2 m cada) até 6 m
        //  Entrada pelo canto SUDOESTE (chão). Lance 1 (sul, sobe p/ leste) → patamar SE (2 m) →
        //  lance 2 (leste, sobe p/ norte) → patamar NE (4 m) → lance 3 (norte, sobe p/ oeste) → patamar NO (6 m)
        //  → topo do núcleo (6 m, parapeitos em volta). Nenhum lance fica embaixo de piso (sem bater a cabeça).
        // =================================================================================================
        static void Lookout()
        {
            Vector3 C = LOOKOUT;
            const float a = LK_A, w = LK_W, o = LK_A + LK_W;
            const float R = 0.25f, RUN = 0.55f;   // 8 degraus x 0,25 = 2 m por lance, 8 x 0,55 = 4,4 m = 2a
            const int N = 8;
            const float TOP = LOOKOUT_TOP;
            float X(float lx) => C.x + lx;
            float Z(float lz) => C.z + lz;

            // núcleo (inclui a faixa oeste) e bases dos lances/patamares — tudo maciço até o chão
            var core = Platform("Mirante_Nucleo", X(-o), X(a), Z(-a), Z(a), TOP, texBrick, 2f, STONE_GREY, texTile, 3f, TILE_SLATE, 0.16f, 0f);
            D.MarkOccluder(core.gameObject);
            Platform("Mirante_Patamar_SE", X(a), X(o), Z(-o), Z(-a), 2f, texBrick, 2f, STONE_GREY, texTile, 2.5f, STEP_STONE, 0.14f, 0f);
            Platform("Mirante_Base_Leste", X(a), X(o), Z(-a), Z(a), 2f, texBrick, 2f, STONE_GREY, texTile, 2.5f, STEP_STONE, 0.14f, 0f);
            Platform("Mirante_Patamar_NE", X(a), X(o), Z(a), Z(o), 4f, texBrick, 2f, STONE_GREY, texTile, 2.5f, STEP_STONE, 0.14f, 0f);
            Platform("Mirante_Base_Norte", X(-a), X(a), Z(a), Z(o), 4f, texBrick, 2f, STONE_GREY, texTile, 2.5f, STEP_STONE, 0.14f, 0f);
            Platform("Mirante_Patamar_NO", X(-o), X(-a), Z(a), Z(o), TOP, texBrick, 2f, STONE_GREY, texTile, 3f, TILE_SLATE, 0.16f, 0f);

            // lances (o lado direito de quem sobe é sempre o de fora → parapeito de pedra)
            Stairs("Mirante_Lance1", new Vector3(X(-a), 0f, Z(-a - w * 0.5f)), Vector3.right, w, N, R, RUN, texTile, 2.5f, STEP_STONE, RAIL_R, true);
            Stairs("Mirante_Lance2", new Vector3(X(a + w * 0.5f), 2f, Z(-a)), Vector3.forward, w, N, R, RUN, texTile, 2.5f, STEP_STONE, RAIL_R, true);
            Stairs("Mirante_Lance3", new Vector3(X(a), 4f, Z(a + w * 0.5f)), Vector3.left, w, N, R, RUN, texTile, 2.5f, STEP_STONE, RAIL_R, true);

            // parapeitos dos patamares e do topo
            var par = Root("Mirante_Parapeitos", gCon);
            const float e = 0.17f;
            Rail(par, new Vector3(X(a), 2f, Z(-o + e)), new Vector3(X(o - e), 2f, Z(-o + e)), true);
            Rail(par, new Vector3(X(o - e), 2f, Z(-o + e)), new Vector3(X(o - e), 2f, Z(-a)), true);
            Rail(par, new Vector3(X(o - e), 4f, Z(a)), new Vector3(X(o - e), 4f, Z(o - e)), true);
            Rail(par, new Vector3(X(o - e), 4f, Z(o - e)), new Vector3(X(a), 4f, Z(o - e)), true);
            Rail(par, new Vector3(X(-a), TOP, Z(o - e)), new Vector3(X(-o + e), TOP, Z(o - e)), true);
            Rail(par, new Vector3(X(-o + e), TOP, Z(o - e)), new Vector3(X(-o + e), TOP, Z(a)), true);
            Rail(par, new Vector3(X(-o + e), TOP, Z(a)), new Vector3(X(-o + e), TOP, Z(-a + e)), true);
            Rail(par, new Vector3(X(-o + e), TOP, Z(-a + e)), new Vector3(X(a - e), TOP, Z(-a + e)), true);
            Rail(par, new Vector3(X(a - e), TOP, Z(-a + e)), new Vector3(X(a - e), TOP, Z(a - e)), true);
            Rail(par, new Vector3(X(a - e), TOP, Z(a - e)), new Vector3(X(-a), TOP, Z(a - e)), true);
            // ameias nos cantos do topo
            foreach (var cc in new[] { new Vector2(-o + e, -a + e), new Vector2(a - e, -a + e), new Vector2(a - e, a - e), new Vector2(-o + e, o - e) })
                BoxMM(par, X(cc.x) - 0.3f, X(cc.x) + 0.3f, TOP + 0.9f, TOP + 1.45f, Z(cc.y) - 0.3f, Z(cc.y) + 0.3f, texBrick, 2f, STONE_GREY, false, "Ameia");

            // topo: braseiro, bandeira e banco virado para a praça
            D.Brazier(gProps, gLuz, new Vector3(X(1.2f), TOP, Z(-1.3f)), 3f, 10f);
            PropC("flag_blue", new Vector3(X(1.3f), TOP, Z(1.3f)), 225f, LevelBuilder.H_FLAG + 0.8f);
            Bench(new Vector3(X(-1.0f), TOP, Z(-1.45f)), Vector3.back);

            // fachada (sul e oeste, viradas para a câmera): estandartes, seteiras, hera e tocha na entrada
            Banner(new Vector3(X(-3.1f), 5.5f, Z(-a) - 0.02f), Vector3.back, 1.0f, 2.0f, new Color(0.18f, 0.31f, 0.56f), new Color(0.88f, 0.72f, 0.29f));
            Banner(new Vector3(X(0.3f), 5.5f, Z(-a) - 0.02f), Vector3.back, 1.0f, 2.0f, new Color(0.56f, 0.18f, 0.23f), new Color(0.88f, 0.72f, 0.29f));
            D.Slab(gCon, new Vector3(X(-o) - 0.02f, 3.6f, Z(-0.6f)), new Vector3(0.3f, 0.9f, 0.04f), SLIT, -90f);
            D.Slab(gCon, new Vector3(X(-o) - 0.02f, 3.6f, Z(1.0f)), new Vector3(0.3f, 0.9f, 0.04f), SLIT, -90f);
            D.Slab(gCon, new Vector3(X(-1.5f), 3.3f, Z(-a) - 0.02f), new Vector3(0.3f, 0.8f, 0.04f), SLIT, 180f);
            Ivy(new Vector3(X(-o) - 0.04f, 5.8f, Z(1.6f)), Vector3.left, 1.6f, 2.6f);
            D.WallTorch(gProps, gLuz, new Vector3(X(-o), 0f, Z(-1.3f)), Vector3.left, 2.3f, 2.4f, 7f);
            D.Slab(gChao, new Vector3(X(-a - w * 0.5f) - 0.5f, 0.012f, Z(-a - w * 0.5f) - 0.5f), new Vector3(3.2f, 0.02f, 3.2f), DIRT, 45f);
            ReserveRect(X(-o) - 1f, X(o), Z(-o) - 1f, Z(o));
        }

        // =================================================================================================
        //  ADARVE na muralha sul (y 3,35), dos dois lados do portão, com escada encostada no muro
        // =================================================================================================
        static void Ramparts(float wallZ)
        {
            const float T = RAMPART_TOP;
            float zIn = wallZ + 2.5f;    // borda interna do adarve (-32)
            WalkSpan("Adarve_Oeste", -31.5f, -3.6f, wallZ, -12.5f, -10.7f);
            WalkSpan("Adarve_Leste", 3.6f, 22.5f, wallZ, 10.7f, 12.5f);
            // o colisor do muro sul agora vai só até o piso do adarve: o portão (mais alto) ganha o seu
            D.ColliderBox(gCon, new Vector3(0f, 3f, wallZ), new Vector3(6.4f, 6f, 2.6f), 0f, "Colisor_Portao");

            // escadas (13 degraus de ~0,26 m, piso de 0,5 m) + patamares
            int n = 13;
            float rise = T / n;
            Stairs("Adarve_Escada_Oeste", new Vector3(-4.2f, 0f, zIn + 0.95f), Vector3.left, 1.8f, n, rise, 0.5f, texTile, 2.5f, STEP_STONE, RAIL_R, false);
            Stairs("Adarve_Escada_Leste", new Vector3(4.2f, 0f, zIn + 0.95f), Vector3.right, 1.8f, n, rise, 0.5f, texTile, 2.5f, STEP_STONE, RAIL_L, false);
            Platform("Adarve_Patamar_Oeste", -12.5f, -10.7f, zIn, zIn + 1.85f, T, texBrick, 2f, STONE_WALL, texTile, 3f, STEP_STONE, 0.14f, 0f);
            Platform("Adarve_Patamar_Leste", 10.7f, 12.5f, zIn, zIn + 1.85f, T, texBrick, 2f, STONE_WALL, texTile, 3f, STEP_STONE, 0.14f, 0f);
            var rr = Root("Adarve_Corrimaos_Patamares", gCon);
            float zr = zIn + 1.85f - 0.12f;
            Rail(rr, new Vector3(-10.7f, T, zr), new Vector3(-12.38f, T, zr), false);
            Rail(rr, new Vector3(-12.38f, T, zr), new Vector3(-12.38f, T, zIn), false);
            Rail(rr, new Vector3(10.7f, T, zr), new Vector3(12.38f, T, zr), false);
            Rail(rr, new Vector3(12.38f, T, zr), new Vector3(12.38f, T, zIn), false);

            // tochas, bandeiras e cargas no adarve
            float zt = wallZ + 0.1f;
            foreach (float x in new[] { -20f, -6.5f, 7.5f, 19f }) D.StandingTorch(gProps, gLuz, new Vector3(x, T, zt), 2.4f, 7.5f);
            PropC("flag_blue", new Vector3(-27.5f, T, zt), 0f, LevelBuilder.H_FLAG);
            PropC("flag_blue", new Vector3(-11.6f, T, zt), 0f, LevelBuilder.H_FLAG);
            PropC("flag_red", new Vector3(15f, T, zt), 0f, LevelBuilder.H_FLAG);
            PropC("barrel", new Vector3(-14.0f, T, zIn - 0.9f), 0f, LevelBuilder.H_BARREL);
            PropC("crate_A_big", new Vector3(14.2f, T, zIn - 0.9f), 20f, LevelBuilder.H_CRATE);
            D.SpawnSized("rope_bundle_A", gProps, new Vector3(-29.5f, T, zIn - 0.8f), 0f, 0.15f);
        }

        /// <summary>Um trecho de adarve entre xa e xb (com abertura no corrimão interno de gapA a gapB).</summary>
        static void WalkSpan(string name, float xa, float xb, float wallZ, float gapA, float gapB)
        {
            const float T = RAMPART_TOP;
            float zIn = wallZ + 2.5f, zWallIn = wallZ + 1f, zOut = wallZ - 0.6f;
            float zA = wallZ - 1.0f, zB = wallZ - 0.4f;   // parapeito externo
            var root = Root(name, gCon);
            // muro de arrimo encostado na face interna da muralha + piso do adarve por cima do muro
            var body = BoxMM(root, xa, xb, -0.05f, T - 0.14f, zWallIn - 0.1f, zIn, texBrick, 2f, STONE_WALL, false, "Arrimo");
            D.MarkOccluder(body);
            BoxMM(root, xa, xb, T - 0.14f, T + 0.01f, zOut, zIn + 0.07f, texTile, 3f, STEP_STONE, false, "Piso_Adarve");
            D.ColliderBox(root, new Vector3((xa + xb) * 0.5f, T * 0.5f, (zOut + zIn) * 0.5f), new Vector3(xb - xa, T, zIn - zOut), 0f, "Colisor_Adarve");
            // parapeito externo com ameias
            BoxMM(root, xa, xb, T, T + 0.45f, zA, zB, texBrick, 2f, STONE_WALL, false, "Parapeito");
            for (float x = xa + 0.6f; x + 0.9f <= xb - 0.4f; x += 1.8f)
                BoxMM(root, x, x + 0.9f, T + 0.45f, T + 1.05f, zA, zB, texBrick, 2f, STONE_WALL, false, "Ameia");
            D.ColliderBox(root, new Vector3((xa + xb) * 0.5f, T + 0.8f, (zA + zB) * 0.5f), new Vector3(xb - xa, 1.6f, zB - zA + 0.2f), 0f, "Colisor_Ameias");
            // pontas fechadas (não deixa andar pelo topo do muro além do adarve)
            BoxMM(root, xa, xa + 0.45f, T, T + 1.0f, zOut, zIn, texBrick, 2f, STONE_WALL, true, "Ponta");
            BoxMM(root, xb - 0.45f, xb, T, T + 1.0f, zOut, zIn, texBrick, 2f, STONE_WALL, true, "Ponta");
            D.ColliderBox(root, new Vector3(xa + 0.22f, T + 1.0f, (zOut + zIn) * 0.5f), new Vector3(0.45f, 2f, zIn - zOut + 1f), 0f, "Colisor_Ponta");
            D.ColliderBox(root, new Vector3(xb - 0.22f, T + 1.0f, (zOut + zIn) * 0.5f), new Vector3(0.45f, 2f, zIn - zOut + 1f), 0f, "Colisor_Ponta");
            // corrimão interno (madeira), aberto no patamar da escada
            float zr = zIn - 0.12f;
            if (gapA > xa + 0.5f) Rail(root, new Vector3(xa + 0.45f, T, zr), new Vector3(gapA, T, zr), false);
            if (gapB < xb - 0.5f) Rail(root, new Vector3(gapB, T, zr), new Vector3(xb - 0.45f, T, zr), false);
            ReserveRect(xa, xb, zWallIn, zIn + 2.2f);
        }

        // =================================================================================================
        //  COLINAS (elipsoides meio enterrados; MeshCollider; inclinação na base ≈ 34°)
        // =================================================================================================
        static float HillY(Vector3 c, float a, float b, float top, float x, float z)
        {
            float dx = x - c.x, dz = z - c.z;
            float k = 1f - (dx * dx + dz * dz) / (a * a);
            if (k <= 0f) return 0f;
            return Mathf.Max(0f, (top - b) + b * Mathf.Sqrt(k));
        }

        static Vector3 OnHill(Vector3 c, float a, float b, float top, float x, float z) => new Vector3(x, HillY(c, a, b, top, x, z), z);

        static void HillMound(string name, Vector3 c, float a, float b, float top)
        {
            float yc = top - b;
            var g = U.Prim(PrimitiveType.Sphere, gChao, new Vector3(c.x, yc, c.z), new Vector3(2f * a, 2f * b, 2f * a), GRASS_HILL);
            g.name = name;
            g.GetComponent<Renderer>().sharedMaterial = U.Lit(GRASS_HILL, 0.1f);
            g.AddComponent<MeshCollider>().sharedMesh = g.GetComponent<MeshFilter>().sharedMesh;
            float r0 = a * Mathf.Sqrt(Mathf.Max(0f, 1f - (yc * yc) / (b * b)));
            ReserveRect(c.x - r0, c.x + r0, c.z - r0, c.z + r0, 0f);
            // grama e flores na encosta
            for (int i = 0; i < 12; i++)
            {
                float ang = Rf(0f, Mathf.PI * 2f), rad = Rf(0.6f, r0 - 0.4f);
                Vector3 p = OnHill(c, a, b, top, c.x + Mathf.Cos(ang) * rad, c.z + Mathf.Sin(ang) * rad);
                if (i % 3 == 0) U.Prim(PrimitiveType.Sphere, gNat, p + Vector3.up * 0.08f, Vector3.one * 0.16f, Pick(FLOWERS));
                else D.SpawnSized(Pick(GRASSES), gNat, p - Vector3.up * 0.03f, Rf(0, 360), Rf(0.35f, 0.55f));
            }
        }

        static void Hills()
        {
            // nordeste (entre a Torre e a taverna): árvore e banco com vista para a praça
            Vector3 h1 = new Vector3(11f, 0f, 18.5f);
            const float a1 = 6f, b1 = 3f, t1 = 1.2f;
            HillMound("Colina_Nordeste", h1, a1, b1, t1);
            Vector3 tp = OnHill(h1, a1, b1, t1, 12.2f, 20.0f);
            D.MarkOccluder(D.SpawnSized("Tree_2_A_Color1", gNat, tp - Vector3.up * 0.1f, Rf(0, 360), 5.6f));
            D.ColliderBox(gNat, tp + new Vector3(0f, 1.5f, 0f), new Vector3(0.9f, 3f, 0.9f), 0f, "Tronco");
            Bench(OnHill(h1, a1, b1, t1, 9.6f, 17.2f) - Vector3.up * 0.06f, new Vector3(-0.5f, 0f, -1f));
            D.SpawnSized("Bush_2_A_Color1", gNat, OnHill(h1, a1, b1, t1, 13.6f, 17.4f), 0f, 0.9f);

            // sudoeste (entre a praça e as casas do sul): árvore pequena, pedras e moitas
            Vector3 h2 = new Vector3(-6.5f, 0f, -20.5f);
            const float a2 = 5f, b2 = 2.4f, t2 = 1.0f;
            HillMound("Colina_Sudoeste", h2, a2, b2, t2);
            Vector3 tp2 = OnHill(h2, a2, b2, t2, -7.4f, -21.4f);
            D.MarkOccluder(D.SpawnSized("Tree_3_A_Color1", gNat, tp2 - Vector3.up * 0.1f, Rf(0, 360), 4.4f));
            D.ColliderBox(gNat, tp2 + new Vector3(0f, 1.5f, 0f), new Vector3(0.8f, 3f, 0.8f), 0f, "Tronco");
            D.SpawnSized(Pick(ROCKS), gNat, OnHill(h2, a2, b2, t2, -4.6f, -19.6f) - Vector3.up * 0.1f, Rf(0, 360), 0.6f);
            D.SpawnSized(Pick(ROCKS), gNat, OnHill(h2, a2, b2, t2, -8.6f, -18.6f) - Vector3.up * 0.1f, Rf(0, 360), 0.45f);
            D.SpawnSized(Pick(BUSHES), gNat, OnHill(h2, a2, b2, t2, -5.4f, -22.6f), Rf(0, 360), 0.8f);
            Bench(OnHill(h2, a2, b2, t2, -5.4f, -19.1f) - Vector3.up * 0.06f, new Vector3(1f, 0f, 1f));
        }
    }
}
