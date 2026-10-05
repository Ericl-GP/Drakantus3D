using System.Collections.Generic;
using UnityEngine;
using D = Drakantus.LevelDecor;

namespace Drakantus
{
    /// <summary>
    /// Monta o cenário estático de cada mapa (chão, construções, natureza, props, luzes, colisores)
    /// e devolve o LevelInfo com spawns, inimigos, NPCs, portais e iluminação.
    /// Mapas: "town" (Praça de Aster), "guild" (interior da Guilda), "tower_f1" (Bosque Esmeralda),
    /// "tower_f2" (Ruínas Sombrias).
    ///
    /// ESCALAS: medidas com Tools/fbx_bounds.py (saída em Tools/model_sizes.json). Os FBX KayKit
    /// vêm com UnitScaleFactor = 100 (metros). As construções do kit Medieval são de "tabuleiro hexagonal"
    /// (~1 m de altura), por isso ganham escala ~5-6x. Em tempo de execução tudo é normalizado pela
    /// altura real dos meshes (LevelDecor.SpawnSized / SpawnBox), então as constantes abaixo são
    /// ALTURAS-ALVO em metros — ajuste aqui sem medo.
    /// </summary>
    public static class LevelBuilder
    {
        // ------------------------------------------------------------------ alturas-alvo (m)
        // Medieval (tamanho no FBX → escala resultante)
        public const float H_TOWER = 14f;        // building_tower_A_blue 2,19 m → x6,4
        public const float H_CORNER_TOWER = 8f;  // building_tower_B_red 2,49 m → x3,2
        public const float H_HOME_A = 6f;        // building_home_A_* 0,93 m → x6,5 (≈5x5,5 m de base)
        public const float H_HOME_B = 7.2f;      // building_home_B_* 1,28 m → x5,6
        public const float H_TAVERN = 8f;        // building_tavern_red 1,40 m → x5,7
        public const float H_BLACKSMITH = 5.6f;  // building_blacksmith_blue 0,985 m → x5,7
        public const float H_MARKET = 5.2f;      // building_market_yellow 0,98 m → x5,3
        public const float H_CHURCH = 9.5f;      // building_church_blue 1,645 m → x5,8
        public const float H_WINDMILL = 9f;      // building_windmill_green 1,46 m → x6,2
        public const float H_BARRACKS = 8.5f;    // building_barracks_red 1,64 m → x5,2 (Guilda)
        public const float H_WELL = 3.2f;        // building_well_blue 0,83 m → x3,9
        public const float H_WALL = 3.3f;        // wall_straight 2 x 1,1 x 0,8 → x3 (segmento de 6 m)
        public const float WALL_SEG = 6f;
        public const float WALL_THICK = 2.0f;
        public const float H_TENT = 2.6f;        // tent 0,52 → x5
        public const float H_FLAG = 3.6f;        // flag_* 0,28 → x13 (mastro alto)
        public const float H_BARREL = 1.05f;     // barrel 0,21 → x5
        public const float H_CRATE = 1.0f;       // crate_A_big 0,21 → x5
        public const float H_CRATE_S = 0.7f;     // crate_B_small 0,14 → x5
        public const float H_SACK = 0.4f;        // sack 0,065 → x6
        public const float H_TARGET = 1.7f;      // target 0,30 → x5,6
        public const float H_RACK = 1.5f;        // weaponrack 0,24 → x6,2
        public const float H_WHEELBARROW = 1.0f; // wheelbarrow 0,19 → x5,3
        // Tools (vêm ~2x maiores que o resto)
        public const float H_ANVIL = 0.85f;      // anvil 0,8 m → x1,06 (largura 1,7 m)
        public const float H_GRINDSTONE = 1.1f;  // grindstone 1,55 → x0,7
        // Forest (já vêm em metros)
        public const float H_TREE_MIN = 4.5f, H_TREE_MAX = 7f;   // Tree_* 3,5–7,8 m (cidade / fundo)
        public const float F_TREE_MIN = 3.5f, F_TREE_MAX = 5.5f; // floresta da torre: perto da trilha (não cobrem a tela)
        public const float F_TREE_GAP = 1.5f;    // folga extra entre a trilha e as árvores
        public const float F_BUSH_MIN = 0.8f, F_BUSH_MAX = 1.4f; // moitas da floresta
        public const float H_TREECLUSTER = 6f;   // trees_A_large 0,93 → x6,5 (≈12 m de largura)
        public const float H_BUSH = 0.9f;        // Bush_* 0,2–0,8
        public const float H_GRASS = 0.5f;       // Grass_* 0,56–0,94
        public const float H_BIGROCK = 2.6f;     // Rock_1_J 3,36 m
        // Personagem de referência: Knight 2,54 m no FBX (com capacete/acessórios); o herói fica ~1,9 m (CharacterVisual).
        // BlockBits: cubos de 2 m centrados no pivô → viram ladrilhos de 4 m (x2) no chão / blocos de parede.
        public const float TILE = 4f;
        public const float DUNGEON_WALL_H = 3f;
        // Se as portas das casas ficarem viradas para trás no Unity, troque para 180.
        public const float FRONT_YAW = 0f;

        // ------------------------------------------------------------------ cores (chão e formas simples)
        static readonly Color GRASS = new Color(0.43f, 0.62f, 0.33f);
        static readonly Color GRASS_DARK = new Color(0.25f, 0.42f, 0.24f);
        static readonly Color GRASS_LIGHT = new Color(0.40f, 0.60f, 0.32f);
        static readonly Color DIRT = new Color(0.52f, 0.41f, 0.28f);
        static readonly Color STONE = new Color(0.62f, 0.60f, 0.57f);
        static readonly Color STONE_DARK = new Color(0.27f, 0.27f, 0.31f);
        static readonly Color BRICK = new Color(0.55f, 0.36f, 0.30f);
        static readonly Color WOOD = new Color(0.50f, 0.34f, 0.20f);
        static readonly Color WOOD_DARK = new Color(0.32f, 0.21f, 0.13f);
        static readonly Color LAVA = new Color(1f, 0.42f, 0.08f);
        static readonly Color GRAVEL = new Color(0.66f, 0.56f, 0.43f);

        static readonly string[] TREES = { "Tree_1_A_Color1", "Tree_1_B_Color1", "Tree_1_C_Color1", "Tree_2_A_Color1", "Tree_2_B_Color1", "Tree_2_C_Color1", "Tree_3_A_Color1", "Tree_3_B_Color1", "Tree_4_A_Color1", "Tree_4_B_Color1" };
        static readonly string[] BUSHES = { "Bush_1_A_Color1", "Bush_1_C_Color1", "Bush_2_A_Color1", "Bush_3_A_Color1", "Bush_4_A_Color1" };
        static readonly string[] GRASSES = { "Grass_1_A_Color1", "Grass_1_C_Color1", "Grass_2_A_Color1", "Grass_2_C_Color1" };
        static readonly string[] ROCKS_SMALL = { "Rock_1_A_Color1", "Rock_2_A_Color1", "Rock_3_A_Color1", "Rock_1_E_Color1", "Rock_3_F_Color1" };
        static readonly string[] ROCKS_BIG = { "Rock_1_J_Color1", "Rock_2_D_Color1" };

        // ------------------------------------------------------------------ estado da construção
        static System.Random rng = new System.Random(1);
        static readonly List<Vector3> reserved = new List<Vector3>();   // (x, raio, z)
        static Transform gChao, gCon, gNat, gProps, gLuz;

        public static LevelInfo Build(string mapId, Transform root)
        {
            // [Andares] andar salvo pelo Criador de Andares (Resources/Floors/<id>.prefab) tem prioridade
            var floorPrefab = string.IsNullOrEmpty(mapId) ? null : Resources.Load<GameObject>(FloorRegistry.PrefabFolder + mapId);
            if (floorPrefab != null) return FloorRoot.Build(floorPrefab, root);
            reserved.Clear();
            gChao = D.Group(root, "Chao");
            gCon = D.Group(root, "Construcoes");
            gNat = D.Group(root, "Natureza");
            gProps = D.Group(root, "Props");
            gLuz = D.Group(root, "Luzes");
            // no máximo 2 luzes com sombra por mapa (viram spots: 1 mapa de sombra cada); tremor padrão das tochas
            D.shadowBudget = 2;
            D.flickerAmount = 0.18f;
            switch (mapId)
            {
                case "guild": rng = new System.Random(2024); D.SetSunAngles(50f, -30f); return BuildGuild();
                case "tower_f1": rng = new System.Random(101); D.SetSunAngles(58f, -20f); return ScatterBreakables(BuildForest());   // [Quebraveis]
                case "tower_f2": rng = new System.Random(202); D.SetSunAngles(50f, -30f); D.flickerAmount = 0.32f; return ScatterBreakables(BuildRuins());   // [Quebraveis]
                default: rng = new System.Random(1337); D.SetSunAngles(31f, -62f); return BuildTown();   // sol baixo: fim de tarde
            }
        }

        // ------------------------------------------------------------------ utilidades
        static float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static int Ri(int a, int bExcl) => rng.Next(a, bExcl);
        static bool Chance(float p) => rng.NextDouble() < p;
        static T Pick<T>(T[] a) => a[rng.Next(a.Length)];
        static Vector3 V(float x, float z) => new Vector3(x, 0f, z);

        static float YawTo(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from; d.y = 0;
            if (d.sqrMagnitude < 0.0001f) return 180f;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        static void Reserve(Vector3 p, float r) { reserved.Add(new Vector3(p.x, r, p.z)); }

        static bool Free(Vector3 p, float r)
        {
            for (int i = 0; i < reserved.Count; i++)
            {
                var q = reserved[i];
                float dx = p.x - q.x, dz = p.z - q.z, rr = r + q.y;
                if (dx * dx + dz * dz < rr * rr) return false;
            }
            return true;
        }

        /// <summary>Construção grande: normaliza a altura, vira a frente para "face", põe colisor e reserva espaço.</summary>
        static GameObject Building(string id, Vector3 pos, float height, Vector3 face, float reserveR, float shrink = 0.86f)
        {
            float yaw = YawTo(pos, face) + FRONT_YAW;
            var g = D.SpawnSized(id, gCon, pos, yaw, height);
            D.AddCollider(g, shrink, 3f);
            D.MarkOccluder(g);
            Reserve(pos, reserveR);
            return g;
        }

        static GameObject Prop(string id, Vector3 pos, float yaw, float height, bool collider = false, float reserveR = 0f)
        {
            var g = D.SpawnSized(id, gProps, pos, yaw, height);
            if (collider) D.AddCollider(g, 0.85f, 1.2f);
            if (reserveR > 0) Reserve(pos, reserveR);
            return g;
        }

        static GameObject SpawnTree(Vector3 pos, float height, bool collider)
        {
            var g = D.SpawnSized(Pick(TREES), gNat, pos, Rf(0, 360), height);
            D.MarkOccluder(g);
            if (collider) D.ColliderBox(gNat, pos + new Vector3(0, 1.5f, 0), new Vector3(0.9f, 3f, 0.9f), 0f, "Tronco");
            return g;
        }

        static void Npc(LevelInfo info, string id, Vector3 pos, float yaw)
        {
            info.npcs.Add(new NpcSpawn(id, pos, yaw));
            Reserve(pos, 1.2f);
        }

        static void AddEnemy(LevelInfo info, string id, float x, float z, float scale = 1f)
        {
            info.enemies.Add(new EnemySpawn(id, V(x, z), scale));
            Reserve(V(x, z), 1.5f);
        }

        /// <summary>Ladrilho de BlockBits com topo em y = top.</summary>
        static GameObject Tile(string id, float x, float z, float size, float top, Color fallback, float thickness = 0.5f)
        {
            return D.SpawnBox(id, gChao, new Vector3(x, top - thickness * 0.5f, z), new Vector3(size, thickness, size), 90f * Ri(0, 4), fallback);
        }

        /// <summary>Sequência de muros (wall_straight) de a até b, com um colisor único (altura colliderH).</summary>
        static void WallRun(Vector3 a, Vector3 b, float height, bool gateInMiddle, float colliderH = 6f)
        {
            Vector3 d = b - a; d.y = 0;
            float len = d.magnitude;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / WALL_SEG));
            float seg = len / n;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - 90f;   // eixo X local do muro ao longo da linha
            for (int i = 0; i < n; i++)
            {
                Vector3 c = a + d * ((i + 0.5f) / n);
                bool gate = gateInMiddle && i == n / 2;
                string id = gate ? "wall_straight_gate" : "wall_straight";
                float h = gate ? height * 1.3f : height;
                D.MarkOccluder(D.SpawnBox(id, gCon, new Vector3(c.x, h * 0.5f, c.z), new Vector3(seg + 0.05f, h, WALL_THICK), yaw, STONE));
            }
            Vector3 mid = (a + b) * 0.5f;
            D.ColliderBox(gCon, new Vector3(mid.x, colliderH * 0.5f, mid.z), new Vector3(len + WALL_THICK, colliderH, WALL_THICK + 0.4f), yaw, "Colisor_Muralha");
        }

        // =================================================================================================
        //  PRAÇA DE ASTER (hub)
        // =================================================================================================
        const float TOWN_HALF = 34.5f;   // muralha em ±34,5 m

        static LevelInfo BuildTown()
        {
            var info = new LevelInfo
            {
                id = "town", title = "Praça de Aster", subtitle = "Cidade dos Aventureiros", kind = "hub",
                floor = 0, next = "", music = "town", // [Musica]
                // fim de tarde leve: sol dourado e baixo, ambiente quente, névoa cor de pêssego
                ambient = new Color(0.64f, 0.57f, 0.52f), sun = new Color(1f, 0.80f, 0.58f), sunIntensity = 1.2f,
                dark = false, fog = new Color(0.86f, 0.75f, 0.64f), fogDensity = 0.0055f,
                bounds = new Bounds(new Vector3(0, 4, 0), new Vector3(TOWN_HALF * 2 + 6, 20, TOWN_HALF * 2 + 6)),
            };

            // ---- chão
            D.Ground(gChao, Vector3.zero, new Vector2(110, 110), GRASS);
            // caminhos de cascalho: norte (Torre), leste (Guilda, passa pela ponte), sul (portão), oeste (ferreiro)
            Texture2D grav = D.GravelTexture();
            D.TiledSlab(gChao, new Vector3(0, 0.025f - 0.1f, 19.5f), new Vector3(4f, 0.2f, 9f), grav, 3f, GRAVEL);
            D.TiledSlab(gChao, new Vector3(17.5f, 0.025f - 0.1f, 0), new Vector3(11f, 0.2f, 4f), grav, 3f, GRAVEL);
            D.TiledSlab(gChao, new Vector3(0, 0.025f - 0.1f, -24.75f), new Vector3(4f, 0.2f, 16.5f), grav, 3f, GRAVEL);
            D.TiledSlab(gChao, new Vector3(-15f, 0.025f - 0.1f, 8.5f), new Vector3(6f, 0.2f, 3f), grav, 3f, GRAVEL);
            // faixas de terra batida (rampa oeste → mirante, escada leste → deck da taverna, mercado, igreja, moinho)
            D.Slab(gChao, new Vector3(-12.8f, 0.01f, 12.8f), new Vector3(2.4f, 0.02f, 6f), DIRT, -10f);
            D.Slab(gChao, new Vector3(12.1f, 0.01f, 7.2f), new Vector3(2.2f, 0.02f, 5.5f), DIRT, 20f);
            D.Slab(gChao, new Vector3(-17.8f, 0.01f, -4f), new Vector3(2.4f, 0.02f, 4.2f), DIRT);
            D.Slab(gChao, new Vector3(17.7f, 0.01f, -17f), new Vector3(2.6f, 0.02f, 12f), DIRT, 120f);
            D.Slab(gChao, new Vector3(-18.2f, 0.01f, -20f), new Vector3(2.6f, 0.02f, 15f), DIRT, -131f);
            Reserve(V(0, 0), 4f);
            for (float z = 16; z <= 24; z += 4) Reserve(V(0, z), 2.6f);
            for (float x = 12; x <= 22; x += 4) Reserve(V(x, 0), 2.6f);
            for (float z = -18; z >= -32; z -= 4) Reserve(V(0, z), 2.6f);
            for (float x = -18; x <= -14; x += 4) Reserve(V(x, 8.5f), 2.2f);

            // ---- muralha com portão ao sul + torres de canto
            // [Praca] o muro sul tem adarve: o colisor vai só até o piso do adarve (o portão ganha um colisor próprio)
            float h = TOWN_HALF;
            WallRun(V(-h + 1, h), V(h - 1, h), H_WALL, false);
            WallRun(V(-h + 1, -h), V(h - 1, -h), H_WALL, true, TownPlaza.RAMPART_TOP);
            WallRun(V(h, -h + 1), V(h, h - 1), H_WALL, false);
            WallRun(V(-h, -h + 1), V(-h, h - 1), H_WALL, false);
            foreach (var c in new[] { V(-h, -h), V(h, -h), V(-h, h), V(h, h) })
            {
                var t = D.SpawnSized("building_tower_B_red", gCon, c, YawTo(c, Vector3.zero), H_CORNER_TOWER, 0f, true);
                D.AddCollider(t, 0.9f, 3f);
                D.MarkOccluder(t);
            }

            // ---- construções (frente voltada para a praça)
            Vector3 o = Vector3.zero;
            var tower = Building("building_tower_A_blue", V(0, 27.5f), H_TOWER, V(0, 0), 5f, 0.9f);
            Building("building_barracks_red", V(27, 0), H_BARRACKS, o, 5.5f);          // Guilda
            Building("building_tavern_red", V(22, 15.5f), H_TAVERN, o, 4.8f);
            Building("building_blacksmith_blue", V(-23, 15), H_BLACKSMITH, o, 4.8f);
            Building("building_market_yellow", V(-25, -6), H_MARKET, V(0, -6), 5.5f);
            Building("building_church_blue", V(23, -20), H_CHURCH, o, 4.8f);
            Building("building_windmill_green", V(-24, -25), H_WINDMILL, o, 4.6f);
            Building("building_home_B_red", V(-26, 27.5f), H_HOME_B, o, 4f);
            Building("building_home_A_yellow", V(-14, 28), H_HOME_A, V(-14, 0), 3.6f);
            Building("building_home_B_blue", V(13, 28), H_HOME_B, V(13, 0), 4f);
            Building("building_home_A_red", V(26, 28), H_HOME_A, o, 3.6f);
            Building("building_home_A_blue", V(-14.5f, -25.5f), H_HOME_A, V(-14.5f, 0), 3.6f);   // [Praca] afastadas do adarve
            Building("building_home_B_green", V(12, -26.5f), H_HOME_B, V(12, 0), 4f);
            Building("building_home_A_yellow", V(27, -30), H_HOME_A, o, 3.4f);
            Building("building_well_blue", V(-8.5f, -5f), H_WELL, V(0, -8), 2.2f, 0.8f);         // [Praca] poço na praça baixa
            if (tower != null) tower.name = "Torre";

            // ---- [Praca] praça em níveis: terraço da fonte, escadas, mirante, adarve, canal/ponte, decks, colinas
            TownPlaza.Build(gChao, gCon, gProps, gNat, gLuz, Reserve, h);

            // ---- Torre: entrada, guardião, bandeiras e tochas
            D.StandingTorch(gProps, gLuz, V(-2.6f, 22.3f));
            D.StandingTorch(gProps, gLuz, V(4.4f, 22.3f));
            Prop("flag_blue", V(-5f, 23f), 180f, H_FLAG, true);
            Prop("flag_blue", V(5.5f, 23f), 180f, H_FLAG, true);
            Npc(info, "guardiao", V(2.4f, 21.6f), YawTo(V(2.4f, 21.6f), V(0, 8)));
            info.spawns["tower_door"] = V(0, 19.5f);

            // ---- Guilda: portal na porta (a ponte do canal fica no caminho)
            info.portals.Add(new PortalSpawn(V(21.8f, 0), "guild", "entrance", "Entrar na Guilda"));
            info.spawns["guild_door"] = V(19.3f, 0);
            Prop("flag_red", V(22.5f, 4f), -90f, H_FLAG, true);
            Prop("flag_red", V(22.5f, -4f), -90f, H_FLAG, true);
            D.StandingTorch(gProps, gLuz, V(22.2f, 2.6f), 2f);
            D.StandingTorch(gProps, gLuz, V(22.2f, -2.6f), 2f);

            // ---- ferreiro
            Prop("anvil", V(-18.5f, 10.5f), 70f, H_ANVIL, true, 1.4f);
            Prop("grindstone", V(-20.5f, 8.2f), 20f, H_GRINDSTONE, true, 1.4f);
            Prop("weaponrack", V(-17.5f, 14.5f), 120f, H_RACK, true, 1.2f);
            Prop("barrel", V(-27.5f, 9.5f), 0f, H_BARREL, true, 0.8f);
            Prop("bucket_metal", V(-19.5f, 11.8f), 0f, 0.55f);
            Prop("Wood_Log_Stack", V(-28.5f, 12f), 90f, 1.3f, true, 1.6f);
            Npc(info, "bram", V(-16f, 9f), YawTo(V(-16f, 9f), o));

            // ---- mercado (tendas/caixas ficam no deck elevado do TownPlaza)
            Prop("Textiles_Stack_Large_Colored", V(-18.5f, 1.5f), 10f, 0.9f, true, 1.2f);
            Prop("Pallet_Wood_Covered_A", V(-22.5f, 1.8f), 0f, 0.35f);
            Prop("wheelbarrow", V(-12f, -17.2f), 200f, H_WHEELBARROW, true, 1f);
            Npc(info, "mika", V(-17.5f, -3.5f), YawTo(V(-17.5f, -3.5f), o));
            Vector3 darioP = new Vector3(-17.3f, TownPlaza.MARKET_TOP, -11.3f);
            Npc(info, "dario", darioP, YawTo(darioP, o));

            // ---- taverna (o bardo toca no deck elevado)
            Prop("crate_A_big", V(18.2f, 19.5f), 10f, H_CRATE, true, 0.8f);
            Prop("resource_lumber", V(27.5f, 11f), 90f, 0.9f, true, 1.2f);
            Vector3 lirioP = new Vector3(17.2f, TownPlaza.TAVERN_TOP, 10.2f);
            Npc(info, "lirio", lirioP, YawTo(lirioP, o));
            // Armeira na sacada da Guilda (vista para a ponte)
            Vector3 rurikP = new Vector3(20.2f, TownPlaza.BALCONY_TOP, -8.3f);
            Npc(info, "rurik", rurikP, YawTo(rurikP, V(8f, -2f)));

            // ---- campo de treino (sudeste)
            Prop("target", V(7.5f, -20f), 0f, H_TARGET, true, 1.2f);
            Prop("target", V(11.5f, -21f), -15f, H_TARGET, true, 1.2f);
            Prop("weaponrack", V(14.5f, -19.5f), -90f, H_RACK, true, 1.2f);
            Prop("crate_B_small", V(14.5f, -21.3f), 20f, H_CRATE_S);
            Prop("flag_red", V(5.5f, -18.5f), 0f, H_FLAG, true);

            // ---- igreja / casas
            Npc(info, "iria", V(17.5f, -16.5f), YawTo(V(17.5f, -16.5f), o));
            Prop("barrel", V(-10f, -28.6f), 0f, H_BARREL, true, 0.7f);
            Prop("crate_A_big", V(-16.8f, -30.6f), 30f, H_CRATE, true, 0.8f);
            Prop("bucket_water", V(-9.2f, -28.9f), 0f, 0.6f);
            Prop("Wood_Log_Stack", V(17f, -26f), 0f, 1.3f, true, 1.6f);
            Prop("sack", V(16f, 22f), 30f, H_SACK);

            // ---- NPCs da praça (alguns nos níveis de cima)
            Vector3 fountain = TownPlaza.FOUNTAIN;
            Vector3 pipP = new Vector3(4.4f, TownPlaza.TERRACE_TOP, 8.8f);
            Vector3 liaP = new Vector3(4.0f, TownPlaza.TERRACE_TOP, 3.2f);
            Npc(info, "pip", pipP, YawTo(pipP, fountain));
            Npc(info, "lia", liaP, YawTo(liaP, fountain));
            Npc(info, "marta", V(-6.5f, -11.5f), YawTo(V(-6.5f, -11.5f), o));
            Vector3 ezioP = TownPlaza.LookoutTop;   // o veterano vigia a praça do alto do Mirante
            Npc(info, "ezio", ezioP, YawTo(ezioP, V(0, -6)));
            Npc(info, "tomas", V(2.8f, -27.2f), 0f);
            // [Guilda] Lumi (pets) no terraço da fonte; estando em info.npcs, o Game não usa a posição padrão dele
            Vector3 lumiP = new Vector3(-4.8f, TownPlaza.TERRACE_TOP, 8.6f);
            Npc(info, "lumi", lumiP, YawTo(lumiP, fountain));
            Prop("flag_blue", V(-2.6f, -31f), 0f, H_FLAG, true);
            Prop("flag_blue", V(2.6f, -31f), 0f, H_FLAG, true);

            // ---- postes de luz pelos caminhos
            foreach (var p in new[] { V(-11.9f, -16.2f), V(11.9f, -16.2f), V(3f, -22.5f), V(-2.9f, -27.5f),
                                      V(-2.9f, 17.5f), V(17f, 3f), V(-14.5f, 3.2f), V(12.3f, 7.6f) })
            {
                D.LampPost(gProps, gLuz, p);
                D.ColliderBox(gProps, p + new Vector3(0, 1.4f, 0), new Vector3(0.4f, 2.8f, 0.4f), 0f, "Colisor_Poste");
                Reserve(p, 0.8f);
            }

            // ---- natureza dentro da cidade
            ScatterTown(Tree_Town, 26, 2.6f, 15f);
            ScatterTown(p => D.SpawnSized(Pick(BUSHES), gNat, p, Rf(0, 360), Rf(0.6f, 1.2f)), 40, 1f, 13.5f);
            ScatterTown(p => D.SpawnSized(Pick(GRASSES), gNat, p, Rf(0, 360), Rf(0.35f, 0.6f)), 70, 0.4f, 13.5f, false);
            ScatterTown(p => D.SpawnSized(Pick(ROCKS_SMALL), gNat, p, Rf(0, 360), Rf(0.3f, 0.7f)), 10, 0.6f, 14f);

            // ---- floresta fora da muralha (cenário de fundo)
            for (int i = 0; i < 36; i++)
            {
                float ang = i / 36f * Mathf.PI * 2f + Rf(-0.05f, 0.05f);
                float r = Rf(41f, 47f);
                Vector3 p = V(Mathf.Sin(ang) * r, Mathf.Cos(ang) * r);
                if (Mathf.Abs(p.x) < 37.5f && Mathf.Abs(p.z) < 37.5f) p *= 1.12f;
                D.MarkOccluder(D.SpawnSized("trees_A_large", gNat, p, Rf(0, 360), H_TREECLUSTER + Rf(-1f, 1f), 0f, true));
            }
            for (int i = 0; i < 28; i++)
            {
                Vector3 p = V(Rf(-48, 48), Rf(-48, 48));
                if (Mathf.Abs(p.x) < 38f && Mathf.Abs(p.z) < 38f) continue;
                D.MarkOccluder(D.SpawnSized(Pick(TREES), gNat, p, Rf(0, 360), Rf(H_TREE_MIN, H_TREE_MAX)));
            }

            // ---- luz de fim de tarde: poeira dourada flutuando sobre a praça
            D.Dust(gProps, new Vector3(0, 2.4f, -1f), new Vector3(30f, 4f, 34f), new Color(1f, 0.85f, 0.6f, 0.8f), 60);

            // ---- spawns
            info.spawns["default"] = V(0, -6f);
            info.spawns["entrance"] = V(0, -6f);
            info.spawns["gate"] = V(0, -29f);
            return info;
        }

        static GameObject Tree_Town(Vector3 p) => SpawnTree(p, Rf(H_TREE_MIN, 6.2f), true);

        /// <summary>Espalha objetos em lugares livres da cidade (fora da praça, dentro da muralha).</summary>
        static void ScatterTown(System.Func<Vector3, GameObject> make, int count, float radius, float minFromCenter, bool reserve = true)
        {
            int placed = 0, tries = 0;
            float lim = TOWN_HALF - 3f;
            while (placed < count && tries < count * 25)
            {
                tries++;
                Vector3 p = V(Rf(-lim, lim), Rf(-lim, lim));
                if (p.magnitude < minFromCenter) continue;
                if (!Free(p, radius)) continue;
                make(p);
                if (reserve) Reserve(p, radius);
                placed++;
            }
        }

        // =================================================================================================
        //  GUILDA (interior)
        // =================================================================================================
        //  Sala de 24 x 18 m (x -12..12, z -9..9). A câmera vem de -x/-z (yaw 45), então as paredes
        //  NORTE (+z) e LESTE (+x) são altas (4 m) e as paredes OESTE e SUL (lado da câmera) são baixas (1 m).
        static LevelInfo BuildGuild()
        {
            var info = new LevelInfo
            {
                id = "guild", title = "Guilda dos Aventureiros", subtitle = "Salão principal", kind = "interior",
                floor = 0, next = "", music = "guild", // [Musica]
                ambient = new Color(0.36f, 0.29f, 0.24f), sun = new Color(1f, 0.82f, 0.6f), sunIntensity = 0.45f,
                dark = false, fog = new Color(0.12f, 0.09f, 0.07f), fogDensity = 0.012f,
                bounds = new Bounds(new Vector3(0, 2, 0), new Vector3(28, 10, 22)),
            };
            const float HX = 12f, HZ = 9f, WH = 4f;

            // ---- chão: madeira 3 m + base escura em volta (para não mostrar o vazio)
            D.Ground(gChao, Vector3.zero, new Vector2(60, 50), new Color(0.08f, 0.06f, 0.05f));
            for (float x = -HX + 1.5f; x < HX; x += 3f)
                for (float z = -HZ + 1.5f; z < HZ; z += 3f)
                    D.SpawnBox("wood", gChao, new Vector3(x, 0.03f - 0.25f, z), new Vector3(3f, 0.5f, 3f), 90f * Ri(0, 2), WOOD);
            // tapetes
            D.Slab(gChao, new Vector3(0, 0.05f, -3.5f), new Vector3(3.2f, 0.02f, 10f), U.Hex("8a2f2f"));
            D.Slab(gChao, new Vector3(0, 0.055f, -3.5f), new Vector3(2.6f, 0.02f, 9.4f), U.Hex("b5463c"));
            D.Disc(gChao, new Vector3(-6f, 0.05f, -2.5f), 2.6f, U.Hex("6a4a7a"));

            // ---- paredes de tijolo (blocos de 2 m)
            for (float x = -HX + 1f; x < HX; x += 2f)
                for (float y = 1f; y < WH; y += 2f)
                    D.SpawnBox(y > 2f && Chance(0.25f) ? "bricks_B" : "bricks_A", gCon, new Vector3(x, y, HZ + 1f), new Vector3(2f, 2f, 2f), 0f, BRICK);
            for (float z = -HZ + 1f; z < HZ + 2f; z += 2f)
                for (float y = 1f; y < WH; y += 2f)
                    D.SpawnBox(y > 2f && Chance(0.25f) ? "bricks_B" : "bricks_A", gCon, new Vector3(HX + 1f, y, z), new Vector3(2f, 2f, 2f), 0f, BRICK);
            for (float z = -HZ - 1f; z < HZ + 2f; z += 2f)
                D.SpawnBox("bricks_A", gCon, new Vector3(-HX - 1f, 0.5f, z), new Vector3(2f, 1f, 2f), 0f, BRICK);
            for (float x = -HX + 1f; x < HX + 2f; x += 2f)
            {
                if (Mathf.Abs(x) < 3f) continue;      // porta
                D.SpawnBox("bricks_A", gCon, new Vector3(x, 0.5f, -HZ - 1f), new Vector3(2f, 1f, 2f), 0f, BRICK);
            }
            // vigas de madeira nas paredes altas
            for (float x = -HX + 4f; x < HX; x += 6f)
                D.SpawnBox("wood", gCon, new Vector3(x, WH * 0.5f, HZ + 0.1f), new Vector3(0.5f, WH, 0.4f), 0f, WOOD_DARK);
            D.SpawnBox("wood", gCon, new Vector3(0, WH + 0.2f, HZ + 1f), new Vector3(2f * HX + 4f, 0.4f, 2.2f), 0f, WOOD_DARK);
            D.SpawnBox("wood", gCon, new Vector3(HX + 1f, WH + 0.2f, 0), new Vector3(2.2f, 0.4f, 2f * HZ + 4f), 0f, WOOD_DARK);
            // colisores das paredes (todas altas para não sair da sala)
            D.ColliderBox(gCon, new Vector3(0, 2.5f, HZ + 1f), new Vector3(2 * HX + 4, 5, 2), 0f, "Colisor_Norte");
            D.ColliderBox(gCon, new Vector3(HX + 1f, 2.5f, 0), new Vector3(2, 5, 2 * HZ + 4), 0f, "Colisor_Leste");
            D.ColliderBox(gCon, new Vector3(-HX - 1f, 2.5f, 0), new Vector3(2, 5, 2 * HZ + 4), 0f, "Colisor_Oeste");
            D.ColliderBox(gCon, new Vector3(-7.5f, 2.5f, -HZ - 1f), new Vector3(11, 5, 2), 0f, "Colisor_Sul_A");
            D.ColliderBox(gCon, new Vector3(7.5f, 2.5f, -HZ - 1f), new Vector3(11, 5, 2), 0f, "Colisor_Sul_B");
            D.ColliderBox(gCon, new Vector3(0, 2.5f, -HZ - 2.5f), new Vector3(8, 5, 1), 0f, "Colisor_Porta");

            // ---- balcão da recepção (Sera)
            var counter = D.SpawnBox("wood", gProps, new Vector3(0, 0.55f, 3f), new Vector3(6f, 1.1f, 0.8f), 0f, WOOD);
            D.AddCollider(counter, 1f, 1.1f);
            D.SpawnBox("wood", gProps, new Vector3(0, 1.15f, 3f), new Vector3(6.4f, 0.12f, 1.1f), 0f, WOOD_DARK);
            var sideA = D.SpawnBox("wood", gProps, new Vector3(-3.5f, 0.55f, 5f), new Vector3(1f, 1.1f, 3.2f), 0f, WOOD);
            D.AddCollider(sideA, 1f, 1.1f);
            var sideB = D.SpawnBox("wood", gProps, new Vector3(3.5f, 0.55f, 5f), new Vector3(1f, 1.1f, 3.2f), 0f, WOOD);
            D.AddCollider(sideB, 1f, 1.1f);
            Prop("crate_B_small", new Vector3(-2.2f, 1.2f, 3f), 20f, 0.45f);
            Prop("lantern", new Vector3(2.4f, 1.2f, 3f), 0f, 0.5f);
            D.PointLight(gLuz, new Vector3(2.4f, 1.9f, 3f), D.TorchColor, 5f, 1.4f, false, true);
            Npc(info, "sera", V(0, 3.8f), 180f);   // colada no balcão para o [E] alcançar pela frente

            // ---- estantes atrás do balcão
            foreach (float x in new[] { -6.5f, -2f, 2f, 6.5f })
            {
                var sh = D.SpawnBox("wood", gProps, new Vector3(x, 1.5f, HZ - 0.45f), new Vector3(2.6f, 3f, 0.8f), 0f, WOOD_DARK);
                D.AddCollider(sh, 1f, 1f);
                for (int k = 0; k < 3; k++)
                {
                    float y = 0.6f + k * 0.95f;
                    D.SpawnBox("wood", gProps, new Vector3(x, y, HZ - 0.95f), new Vector3(2.5f, 0.08f, 0.35f), 0f, WOOD);
                    string pid = Pick(new[] { "crate_B_small", "barrel", "sack", "bucket_water" });
                    Prop(pid, new Vector3(x + Rf(-0.8f, 0.8f), y + 0.04f, HZ - 1.0f), Rf(0, 360), 0.4f);
                }
            }
            // ---- quadro de missões (parede norte, à esquerda) + Aldra
            var board = D.SpawnBox("wood", gProps, new Vector3(-9.5f, 2.1f, HZ - 0.1f), new Vector3(3.2f, 2f, 0.2f), 0f, WOOD_DARK);
            Color[] papers = { U.Hex("f2e6c8"), U.Hex("e8d49a"), U.Hex("f5f0e0"), U.Hex("d9c8a0") };
            for (int i = 0; i < 7; i++)
            {
                var p = D.Slab(gProps, new Vector3(-10.7f + (i % 4) * 0.8f + Rf(-0.1f, 0.1f), 2.55f - (i / 4) * 0.85f + Rf(-0.1f, 0.1f), HZ - 0.22f),
                               new Vector3(0.55f, 0.65f, 0.02f), papers[i % papers.Length], Rf(-8f, 8f));
                p.name = "Missao";
            }
            if (board != null) board.name = "Quadro_Missoes";
            Npc(info, "aldra", V(-9.5f, 6.6f), 160f);

            // ---- estandartes (lajes coloridas) e escudos na parede
            foreach (var bx in new[] { -4.2f, 4.2f })
            {
                D.Slab(gProps, new Vector3(bx, 2.6f, HZ - 0.05f), new Vector3(1.3f, 2.4f, 0.06f), U.Hex(bx < 0 ? "7a2a3a" : "2a4a8a"));
                D.Slab(gProps, new Vector3(bx, 3.85f, HZ - 0.08f), new Vector3(1.5f, 0.12f, 0.1f), U.Hex("d4a640"));
                Prop("shield_badge", new Vector3(bx, 2.5f, HZ - 0.25f), 180f, 0.8f);
            }
            Prop("flag_red", new Vector3(-HX + 1.2f, 0, HZ - 1.2f), 135f, 3.2f, true);
            Prop("flag_blue", new Vector3(HX - 1.2f, 0, -HZ + 1.6f), -45f, 3.2f, true);

            // ---- lareira (canto nordeste)
            var hearth = D.SpawnBox("stone_dark", gProps, new Vector3(HX - 0.9f, 0.7f, 6.8f), new Vector3(1.6f, 1.4f, 3f), 0f, STONE_DARK);
            D.AddCollider(hearth, 1f, 1.4f);
            D.SpawnBox("bricks_B", gProps, new Vector3(HX - 0.4f, 2.6f, 6.8f), new Vector3(0.8f, 2.6f, 2.4f), 0f, BRICK);
            D.Flame(gProps, new Vector3(HX - 1.2f, 1.45f, 6.4f), 1.6f, D.FlameColor, 18);
            D.Flame(gProps, new Vector3(HX - 1.2f, 1.45f, 7.2f), 1.4f, D.FlameColor, 14);
            D.PointLight(gLuz, new Vector3(HX - 2.4f, 2f, 6.8f), D.TorchColor, 10f, 3.2f, true, true);

            // ---- mesas dos aventureiros (madeira + barris como bancos)
            foreach (var t in new[] { V(-6f, -2.5f), V(6f, -5.5f) })
            {
                var tb = D.SpawnBox("wood", gProps, t + new Vector3(0, 0.45f, 0), new Vector3(2.2f, 0.9f, 1.3f), 0f, WOOD);
                D.AddCollider(tb, 1f, 0.9f);
                Prop("barrel", t + new Vector3(-1.6f, 0, 0), 0f, 0.7f, true);
                Prop("barrel", t + new Vector3(1.6f, 0, 0.2f), 0f, 0.7f, true);
                Prop("bucket_metal", t + new Vector3(0.3f, 0.9f, 0.1f), 0f, 0.28f);
                Prop("crate_B_small", t + new Vector3(-0.5f, 0.9f, -0.2f), 30f, 0.25f);
            }
            Npc(info, "nyx", V(-6f, -4.2f), 0f);
            Npc(info, "borin", V(-3.6f, -0.8f), 235f);

            // ---- canto do instrutor (leste): suporte de armas + alvo
            Prop("weaponrack", new Vector3(HX - 0.7f, 0, -1.5f), -90f, H_RACK, true);
            Prop("weaponrack", new Vector3(HX - 0.7f, 0, 1.2f), -90f, H_RACK, true);
            Prop("target", new Vector3(HX - 1.2f, 0, -6.6f), -60f, H_TARGET, true);
            Npc(info, "kael",V(8.8f, -1.2f), YawTo(V(8.8f, -1.2f), V(0, -3)));
            Npc(info, "oren", V(6.5f, 6f), 200f);
            Prop("crate_A_big", new Vector3(-HX + 1f, 0, -HZ + 1.2f), 10f, H_CRATE, true);
            Prop("crate_B_small", new Vector3(-HX + 1.2f, H_CRATE, -HZ + 1.3f), 40f, H_CRATE_S);
            Prop("barrel", new Vector3(-HX + 2.3f, 0, -HZ + 1f), 0f, H_BARREL, true);
            Prop("rope_bundle_A", new Vector3(8.5f, 0, 8f), 0f, 0.15f);

            // ---- tochas de parede (norte e leste) + luz geral
            foreach (float x in new[] { -7f, 0f, 7f })
                D.WallTorch(gProps, gLuz, new Vector3(x, 0, HZ), Vector3.back, 2.4f, 2.6f, 8f);
            foreach (float z in new[] { -5.5f, 1f })
                D.WallTorch(gProps, gLuz, new Vector3(HX, 0, z), Vector3.left, 2.4f, 2.6f, 8f);
            D.PointLight(gLuz, new Vector3(-8f, 3.2f, -5f), D.TorchColor, 9f, 1.6f, false, false);
            D.Motes(gProps, new Vector3(0, 2f, 0), new Vector3(20f, 3f, 14f), new Color(1f, 0.8f, 0.5f, 0.5f), 25, 0.01f);

            // ---- saída
            info.portals.Add(new PortalSpawn(V(0, -8.2f), "town", "guild_door", "Sair para a Praça"));
            info.spawns["entrance"] = V(0, -5.8f);
            info.spawns["default"] = V(0, -5.8f);
            return info;
        }

        // =================================================================================================
        //  ANDAR 01 · BOSQUE ESMERALDA
        // =================================================================================================
        struct Clearing { public Vector3 c; public float r; public Clearing(float x, float z, float r) { c = new Vector3(x, 0, z); this.r = r; } }

        static readonly Clearing[] F1_ROOMS =
        {
            new Clearing(0, 0, 8f),      // 0 entrada
            new Clearing(6, 21, 9f),     // 1 lacaios
            new Clearing(-4, 42, 10f),   // 2
            new Clearing(10, 61, 10f),   // 3
            new Clearing(0, 83, 12f),    // 4 chefe (Rei Esqueleto)
            new Clearing(8, 103, 7.5f),  // 5 santuário
        };
        const float F1_TRAIL_W = 5f;

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a, ap = p - a; ab.y = 0; ap.y = 0;
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            Vector3 q = a + ab * t; q.y = 0;
            Vector3 d = p - q; d.y = 0;
            return d.magnitude;
        }

        /// <summary>Distância com sinal até a área andável (negativo = dentro).</summary>
        static float ForestSdf(Vector3 p)
        {
            float d = 1e9f;
            for (int i = 0; i < F1_ROOMS.Length; i++)
            {
                Vector3 q = p - F1_ROOMS[i].c; q.y = 0;
                d = Mathf.Min(d, q.magnitude - F1_ROOMS[i].r);
                if (i > 0) d = Mathf.Min(d, SegDist(p, F1_ROOMS[i - 1].c, F1_ROOMS[i].c) - F1_TRAIL_W * 0.5f);
            }
            return d;
        }

        static LevelInfo BuildForest()
        {
            var info = new LevelInfo
            {
                id = "tower_f1", title = "Torre de Aster", subtitle = "Andar 01 · Bosque Esmeralda", kind = "dungeon",
                floor = 1, next = "tower_f2", music = "forest", // [Musica]
                // sol alto e quente filtrado pelas copas; sombra/névoa verde-azulada para dar profundidade
                ambient = new Color(0.36f, 0.46f, 0.40f), sun = new Color(1f, 0.94f, 0.76f), sunIntensity = 0.95f,
                dark = false, fog = new Color(0.30f, 0.45f, 0.37f), fogDensity = 0.02f,
            };
            // limites
            float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
            foreach (var r in F1_ROOMS)
            {
                minX = Mathf.Min(minX, r.c.x - r.r); maxX = Mathf.Max(maxX, r.c.x + r.r);
                minZ = Mathf.Min(minZ, r.c.z - r.r); maxZ = Mathf.Max(maxZ, r.c.z + r.r);
            }
            Vector3 center = new Vector3((minX + maxX) * 0.5f, 0, (minZ + maxZ) * 0.5f);
            info.bounds = new Bounds(center + Vector3.up * 3f, new Vector3(maxX - minX + 8f, 16f, maxZ - minZ + 8f));
            float margin = 16f;
            D.Ground(gChao, center, new Vector2(maxX - minX + margin * 2f, maxZ - minZ + margin * 2f), GRASS_DARK);

            // clareiras e trilhas (cores)
            for (int i = 0; i < F1_ROOMS.Length; i++)
            {
                var r = F1_ROOMS[i];
                D.Disc(gChao, r.c + new Vector3(0, 0.01f, 0), r.r + 1.2f, GRASS_LIGHT);
                if (i > 0)
                {
                    Vector3 a = F1_ROOMS[i - 1].c, b = r.c, d = b - a;
                    D.Slab(gChao, (a + b) * 0.5f + new Vector3(0, 0.02f, 0), new Vector3(F1_TRAIL_W - 1.2f, 0.02f, d.magnitude), DIRT, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                    // pedrinhas de cascalho ao longo da trilha
                    int steps = Mathf.RoundToInt(d.magnitude / 3f);
                    for (int s = 1; s < steps; s++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, s / (float)steps) + new Vector3(Rf(-1.2f, 1.2f), 0, Rf(-1.2f, 1.2f));
                        if (Chance(0.5f)) D.SpawnSized(Pick(ROCKS_SMALL), gNat, p, Rf(0, 360), Rf(0.12f, 0.25f));
                    }
                }
            }
            D.Disc(gChao, F1_ROOMS[4].c + new Vector3(0, 0.025f, 0), 6f, U.Hex("5c6b45"));   // arena do chefe
            D.Disc(gChao, F1_ROOMS[5].c + new Vector3(0, 0.025f, 0), 4f, U.Hex("8fbf6a"));   // santuário

            // ---- spawns / portal
            info.spawns["entrance"] = V(0, 1f);
            info.spawns["default"] = V(0, 1f);
            info.portals.Add(new PortalSpawn(V(0, -4.5f), "town", "tower_door", "Sair da Torre"));
            Reserve(V(0, -4.5f), 2.5f); Reserve(V(0, 1f), 2.5f);

            // ---- inimigos (ficam mais difíceis)
            AddEnemy(info, "minion", 3, 20); AddEnemy(info, "minion", 8.5f, 22.5f); AddEnemy(info, "minion", 6, 17); AddEnemy(info, "archer", 10, 26);
            AddEnemy(info, "minion", -8, 39); AddEnemy(info, "minion", 0, 38); AddEnemy(info, "warrior", -5, 44); AddEnemy(info, "warrior", 0.5f, 45);
            AddEnemy(info, "rogue", -9, 46); AddEnemy(info, "archer", -3, 49);
            AddEnemy(info, "swarm_ossinho", -6, 36.5f); AddEnemy(info, "swarm_ossinho", -4.5f, 35.5f); AddEnemy(info, "swarm_ossinho", -2.5f, 36f);   // [Inimigos] bando de ossinhos
            AddEnemy(info, "bomber", -10, 43);   // [Inimigos] bombardeiro
            AddEnemy(info, "warrior", 6, 59); AddEnemy(info, "shield_bearer", 14, 63); AddEnemy(info, "rogue", 8, 66); AddEnemy(info, "ice_rogue", 15, 57);   // [Inimigos] escudeiro e glacial (eram: guerreiro, ladino)
            AddEnemy(info, "mage", 11, 68); AddEnemy(info, "brute", 10, 61);
            AddEnemy(info, "charger", 4, 64); AddEnemy(info, "healer", 13, 67);   // [Inimigos] aríete e curandeiro
            AddEnemy(info, "king", 0, 87); AddEnemy(info, "minion", -5, 81); AddEnemy(info, "minion", 5, 81); AddEnemy(info, "warrior", 0, 77);
            info.hasSanctuary = true;
            info.sanctuary = F1_ROOMS[5].c + new Vector3(0, 0, 1f);
            Reserve(info.sanctuary, 3f);
            foreach (var r in F1_ROOMS) Reserve(r.c, 2f);

            // ---- obstáculos grandes dentro das clareiras
            BigRock(V(-11.5f, 37.5f), 2.8f);
            BigRock(V(3.5f, 51.5f), 2.4f);
            BigRock(V(18f, 66f), 2.2f);
            BigRock(V(-9f, 88f), 2.6f);
            BigRock(V(9.5f, 88.5f), 2.3f);
            var ruin = D.SpawnSized("building_destroyed", gCon, V(-3f, 57f), 40f, 3.2f, 0f, true);
            D.AddCollider(ruin, 0.8f, 2f);
            D.MarkOccluder(ruin);
            Reserve(V(-3f, 57f), 4f);
            Prop("Stone_Chunks_Large", V(4f, 6f), 30f, 0.8f, true, 1.2f);
            Prop("Wood_Log_Stack", V(-5f, 3f), 60f, 1.2f, true, 1.5f);
            Prop("rock_single_C", V(15f, 18f), 0f, 0.6f);

            // ---- decoração das clareiras (grama, flores, pedrinhas, arbustos)
            for (int i = 0; i < F1_ROOMS.Length; i++)
            {
                var r = F1_ROOMS[i];
                for (int k = 0; k < 14; k++)
                {
                    float a = Rf(0, Mathf.PI * 2f), rad = Rf(2.5f, r.r - 0.5f);
                    Vector3 p = r.c + new Vector3(Mathf.Cos(a) * rad, 0, Mathf.Sin(a) * rad);
                    if (!Free(p, 0.4f)) continue;
                    D.SpawnSized(Pick(GRASSES), gNat, p, Rf(0, 360), Rf(0.35f, 0.6f));
                }
                for (int k = 0; k < 4; k++)
                {
                    float a = Rf(0, Mathf.PI * 2f), rad = Rf(3f, r.r - 1f);
                    Vector3 p = r.c + new Vector3(Mathf.Cos(a) * rad, 0, Mathf.Sin(a) * rad);
                    if (!Free(p, 0.8f)) continue;
                    if (Chance(0.5f)) D.SpawnSized(Pick(BUSHES), gNat, p, Rf(0, 360), Rf(F_BUSH_MIN, F_BUSH_MAX));
                    else D.SpawnSized(Pick(ROCKS_SMALL), gNat, p, Rf(0, 360), Rf(0.25f, 0.5f));
                }
                D.Motes(gNat, r.c + new Vector3(0, 1.6f, 0), new Vector3(r.r * 1.6f, 2.5f, r.r * 1.6f), new Color(0.75f, 1f, 0.45f, 1f), 18, 0.02f);
                // raios de sol entre as copas + poeira suspensa brilhando neles
                D.LightShafts(gNat, r.c + new Vector3(0, 5.5f, 0), new Vector3(r.r * 1.3f, 1f, r.r * 1.3f), new Color(1f, 0.93f, 0.68f, 0.10f), i == 4 ? 6 : 4);
                D.Dust(gNat, r.c + new Vector3(0, 2.2f, 0), new Vector3(r.r * 1.5f, 3.5f, r.r * 1.5f), new Color(1f, 0.96f, 0.82f, 0.9f), 45);
            }

            // ---- borda: mata fechada (árvores + colisores invisíveis)
            float step = 3f;
            int trees = 0;
            for (float x = minX - 12f; x <= maxX + 12f; x += step)
                for (float z = minZ - 12f; z <= maxZ + 12f; z += step)
                {
                    Vector3 p = V(x, z);
                    float d = ForestSdf(p);
                    if (d > 0.5f && d < 5.5f)
                        D.ColliderBox(gNat, p + new Vector3(0, 2.5f, 0), new Vector3(step + 0.3f, 5f, step + 0.3f), 0f, "Colisor_Mata");
                    Vector3 pj = p + new Vector3(Rf(-1.2f, 1.2f), 0, Rf(-1.2f, 1.2f));
                    float dj = ForestSdf(pj);
                    float treeFrom = 0.8f + F_TREE_GAP;   // árvores 1,5 m mais afastadas da trilha
                    if (dj > treeFrom && dj < 10.5f && trees < 520)
                    {
                        if (dj < treeFrom + 2.4f && Chance(0.14f))
                            D.MarkOccluder(D.SpawnSized(Pick(ROCKS_BIG), gNat, pj, Rf(0, 360), Rf(1.4f, 2.6f)));
                        else if (Chance(dj < treeFrom + 4f ? 0.85f : 0.55f))
                        {
                            // perto da trilha: 3,5–5,5 m; mais ao fundo um pouco maiores (silhueta da mata)
                            float th = dj < 6.5f ? Rf(F_TREE_MIN, F_TREE_MAX) : Rf(F_TREE_MIN + 1f, F_TREE_MAX + 1f);
                            D.MarkOccluder(D.SpawnSized(Pick(TREES), gNat, pj, Rf(0, 360), th));
                            trees++;
                        }
                    }
                    else if (dj > -1.2f && dj <= treeFrom && Chance(0.45f))
                    {
                        if (!Free(pj, 0.6f)) continue;
                        D.SpawnSized(Pick(BUSHES), gNat, pj, Rf(0, 360), Rf(F_BUSH_MIN, F_BUSH_MAX));
                    }
                }

            // ---- luzes: lanternas na entrada, tochas na arena do chefe, santuário iluminado
            D.LampPost(gProps, gLuz, V(-3f, -4f), 2f);
            D.LampPost(gProps, gLuz, V(3f, -4f), 2f);
            D.StandingTorch(gProps, gLuz, V(-4f, 73.5f), 2.6f, 8f);
            D.StandingTorch(gProps, gLuz, V(4f, 73.5f), 2.6f, 8f);
            D.StandingTorch(gProps, gLuz, V(-8f, 92f), 2.6f, 8f, true);
            D.StandingTorch(gProps, gLuz, V(8f, 92f), 2.6f, 8f);
            D.PointLight(gLuz, info.sanctuary + new Vector3(0, 3f, 0), new Color(0.7f, 1f, 0.8f), 10f, 2.2f, false, false);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                Vector3 p = info.sanctuary + new Vector3(Mathf.Cos(a) * 4f, 0, Mathf.Sin(a) * 4f);
                D.SpawnSized(Pick(ROCKS_SMALL), gNat, p, Rf(0, 360), Rf(0.6f, 0.9f));
                D.SpawnSized("Grass_2_C_Color1", gNat, p + new Vector3(0.8f, 0, 0.4f), Rf(0, 360), 0.5f);
            }
            D.Motes(gNat, info.sanctuary + new Vector3(0, 1.5f, 0), new Vector3(6f, 3f, 6f), new Color(0.6f, 1f, 0.9f, 1f), 30, 0.08f);
            return info;
        }

        static void BigRock(Vector3 p, float h)
        {
            var g = D.SpawnSized(Pick(ROCKS_BIG), gNat, p, Rf(0, 360), h);
            D.AddCollider(g, 0.75f, 2f);
            D.MarkOccluder(g);
            Reserve(p, h * 0.8f);
        }

        // =================================================================================================
        //  ANDAR 02 · RUÍNAS SOMBRIAS (masmorra em grade de 4 m)
        // =================================================================================================
        const int GW = 44, GH = 52, OX = 12, OZ = 6;
        static bool[,] carved, lava;

        static bool InGrid(int i, int j) => i + OX >= 0 && i + OX < GW && j + OZ >= 0 && j + OZ < GH;
        static bool IsFloor(int i, int j) => InGrid(i, j) && carved[i + OX, j + OZ];
        static bool IsLava(int i, int j) => InGrid(i, j) && lava[i + OX, j + OZ];

        static void Carve(int x0, int z0, int x1, int z1)
        {
            for (int i = x0; i <= x1; i++) for (int j = z0; j <= z1; j++) if (InGrid(i, j)) carved[i + OX, j + OZ] = true;
        }

        static void MarkLava(int x0, int z0, int x1, int z1)
        {
            for (int i = x0; i <= x1; i++) for (int j = z0; j <= z1; j++) if (InGrid(i, j)) lava[i + OX, j + OZ] = true;
        }

        static Vector3 Cell(float i, float j) => new Vector3(i * TILE, 0, j * TILE);

        static LevelInfo BuildRuins()
        {
            var info = new LevelInfo
            {
                id = "tower_f2", title = "Torre de Aster", subtitle = "Andar 02 · Ruínas Sombrias", kind = "dungeon",
                floor = 2, next = "", music = "crypt", // [Musica]
                // masmorra: ambiente frio azul-violeta contra o laranja das tochas (que tremulam mais aqui)
                ambient = new Color(0.09f, 0.10f, 0.18f), sun = new Color(0.42f, 0.48f, 0.82f), sunIntensity = 0.1f,
                dark = true, fog = new Color(0.035f, 0.04f, 0.08f), fogDensity = 0.026f,
            };
            carved = new bool[GW, GH];
            lava = new bool[GW, GH];

            // ---- planta (células de 4 m)
            Carve(-2, 0, 2, 3);      // salão de entrada
            Carve(-1, 4, 0, 8);      // corredor norte
            Carve(-4, 9, 3, 14);     // Salão A (colunas)
            Carve(4, 11, 9, 12);     // corredor leste
            Carve(10, 8, 16, 16);    // Salão B (lava)
            Carve(12, 17, 13, 21);   // corredor norte
            Carve(8, 22, 17, 27);    // Salão C
            Carve(2, 24, 7, 25);     // corredor oeste
            Carve(-6, 21, 1, 29);    // Salão do Necromante
            Carve(-3, 30, -2, 32);   // corredor do santuário
            Carve(-5, 33, 0, 36);    // santuário
            MarkLava(12, 10, 13, 11);
            MarkLava(15, 14, 16, 15);
            MarkLava(0, 21, 1, 22);
            MarkLava(-6, 28, -5, 29);

            // ---- limites
            int mi = 999, ma = -999, mj = 999, mx = -999;
            for (int i = -OX; i < GW - OX; i++)
                for (int j = -OZ; j < GH - OZ; j++)
                    if (IsFloor(i, j)) { mi = Mathf.Min(mi, i); ma = Mathf.Max(ma, i); mj = Mathf.Min(mj, j); mx = Mathf.Max(mx, j); }
            Vector3 c0 = Cell(mi - 1, mj - 1), c1 = Cell(ma + 1, mx + 1);
            Vector3 center = (c0 + c1) * 0.5f;
            info.bounds = new Bounds(center + Vector3.up * 2f, new Vector3(c1.x - c0.x + TILE, 12f, c1.z - c0.z + TILE));
            D.Ground(gChao, center, new Vector2(c1.x - c0.x + 40f, c1.z - c0.z + 40f), new Color(0.05f, 0.05f, 0.07f));

            // ---- chão, lava e paredes
            var wallCells = new List<Vector2Int>();
            for (int i = -OX; i < GW - OX; i++)
                for (int j = -OZ; j < GH - OZ; j++)
                {
                    Vector3 p = Cell(i, j);
                    if (IsFloor(i, j))
                    {
                        if (IsLava(i, j))
                        {
                            D.SpawnBox("lava", gChao, p + new Vector3(0, -0.49f, 0), new Vector3(TILE, 1f, TILE), 0f, LAVA, new Color(2f, 0.7f, 0.1f));
                            D.ColliderBox(gChao, p + new Vector3(0, 1f, 0), new Vector3(TILE, 2f, TILE), 0f, "Colisor_Lava");
                        }
                        else
                        {
                            string fid = Chance(0.18f) ? "stone" : "stone_dark";
                            Tile(fid, p.x, p.z, TILE, 0.02f, STONE_DARK, 1f);
                        }
                        continue;
                    }
                    bool edge = false;
                    for (int di = -1; di <= 1 && !edge; di++)
                        for (int dj = -1; dj <= 1; dj++)
                            if (IsFloor(i + di, j + dj)) { edge = true; break; }
                    if (!edge) continue;
                    string wid = Chance(0.2f) ? "bricks_B" : (Chance(0.15f) ? "stone_dark" : "bricks_A");
                    var w = D.SpawnBox(wid, gCon, p + new Vector3(0, DUNGEON_WALL_H * 0.5f, 0), new Vector3(TILE, DUNGEON_WALL_H, TILE), 90f * Ri(0, 4), BRICK);
                    D.AddCollider(w, 1f, 0f);
                    D.MarkOccluder(w);
                    wallCells.Add(new Vector2Int(i, j));
                }

            // ---- lava: brilho e brasas (1 luz por poça)
            foreach (var pool in new[] { new Vector4(12, 10, 13, 11), new Vector4(15, 14, 16, 15), new Vector4(0, 21, 1, 22), new Vector4(-6, 28, -5, 29) })
            {
                Vector3 pc = Cell((pool.x + pool.z) * 0.5f, (pool.y + pool.w) * 0.5f);
                D.PointLight(gLuz, pc + new Vector3(0, 1.2f, 0), new Color(1f, 0.45f, 0.12f), 10f, 3.2f, false, true);
                D.Motes(gProps, pc + new Vector3(0, 0.8f, 0), new Vector3(7f, 1f, 7f), new Color(1f, 0.5f, 0.15f, 1f), 24, 0.25f);
            }

            // ---- tochas nas paredes (face voltada para o chão vizinho)
            for (int k = wallCells.Count - 1; k > 0; k--) { int r = Ri(0, k + 1); var tmp = wallCells[k]; wallCells[k] = wallCells[r]; wallCells[r] = tmp; }
            var torchSpots = new List<Vector3>();
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            int lights = 0;
            foreach (var wc in wallCells)
            {
                if (lights >= 26) break;
                foreach (var dd in dirs)
                {
                    int ni = wc.x + dd.x, nj = wc.y + dd.y;
                    if (!IsFloor(ni, nj) || IsLava(ni, nj)) continue;
                    Vector3 dir = new Vector3(dd.x, 0, dd.y);
                    Vector3 face = Cell(wc.x, wc.y) + dir * (TILE * 0.5f);
                    bool near = false;
                    foreach (var t in torchSpots) if ((t - face).sqrMagnitude < 12f * 12f) { near = true; break; }
                    if (near) break;
                    torchSpots.Add(face);
                    D.WallTorch(gProps, gLuz, face, dir, 2.1f, 2.8f, 8.5f);
                    lights++;
                    break;
                }
            }

            // ---- colunas
            var pillars = new List<Vector3>
            {
                V(-10, 40), V(6, 40), V(-10, 52), V(6, 52),                    // Salão A
                V(42, 90), V(58, 90), V(42, 106), V(58, 106),                  // Salão C
                V(-18, 90), V(-2, 90), V(-18, 100), V(-2, 100), V(-18, 110), V(-2, 110), // Necromante
            };
            foreach (var p in pillars)
            {
                var col = D.SpawnBox("bricks_B", gCon, p + new Vector3(0, 1.6f, 0), new Vector3(1.4f, 3.2f, 1.4f), 0f, BRICK);
                D.AddCollider(col, 1f, 0f);
                D.MarkOccluder(col);
                D.SpawnBox("stone_dark", gCon, p + new Vector3(0, 0.2f, 0), new Vector3(1.9f, 0.4f, 1.9f), 0f, STONE_DARK);
                D.SpawnBox("stone_dark", gCon, p + new Vector3(0, 3.35f, 0), new Vector3(1.9f, 0.3f, 1.9f), 0f, STONE_DARK);
                Reserve(p, 1.6f);
            }

            // ---- braseiros no salão do chefe
            D.Brazier(gProps, gLuz, V(-14, 92), 3.4f, 10f, true);
            D.Brazier(gProps, gLuz, V(-6, 92), 3.2f, 10f);
            D.Brazier(gProps, gLuz, V(-14, 112), 3.2f, 10f);
            D.Brazier(gProps, gLuz, V(-6, 112), 3.2f, 10f);
            foreach (var p in new[] { V(-14, 92), V(-6, 92), V(-14, 112), V(-6, 112) }) Reserve(p, 1.2f);
            D.Disc(gChao, V(-10, 102) + new Vector3(0, 0.02f, 0), 5f, U.Hex("3a1f3f"));
            D.Motes(gProps, V(-10, 102) + new Vector3(0, 1.5f, 0), new Vector3(14f, 3f, 14f), new Color(0.6f, 0.4f, 1f, 1f), 30, 0.05f);

            // ---- spawns / portal / inimigos
            info.spawns["entrance"] = V(0, 6f);
            info.spawns["default"] = V(0, 6f);
            info.portals.Add(new PortalSpawn(V(0, 0.5f), "town", "tower_door", "Sair da Torre"));
            Reserve(V(0, 0.5f), 2.5f); Reserve(V(0, 6f), 2.5f);
            D.StandingTorch(gProps, gLuz, V(-3.5f, 1f), 2.4f, 8f);
            D.StandingTorch(gProps, gLuz, V(3.5f, 1f), 2.4f, 8f);

            // Salão A
            AddEnemy(info, "warrior", -6, 44); AddEnemy(info, "warrior", 3, 48); AddEnemy(info, "rogue", -12, 50); AddEnemy(info, "rogue", 9, 44);
            AddEnemy(info, "archer", -2, 54); AddEnemy(info, "plague_minion", -13, 40); AddEnemy(info, "bomber", 10, 54);   // [Inimigos] pestilento e bombardeiro (eram: 2 lacaios)
            AddEnemy(info, "sniper", -8, 53);   // [Inimigos] atirador de elite
            // corredor leste
            AddEnemy(info, "rogue", 26, 46);
            // Salão B (lava)
            AddEnemy(info, "fire_mage", 44, 58); AddEnemy(info, "mage", 60, 48); AddEnemy(info, "archer", 62, 36); AddEnemy(info, "archer", 42, 34);   // [Inimigos] piromante (era: mago)
            AddEnemy(info, "fire_warrior", 56, 52, 1.1f); AddEnemy(info, "brute", 52, 60);   // [Inimigos] flamejante (era: guerreiro)
            AddEnemy(info, "summoner", 56, 40);   // [Inimigos] invocador
            // Salão C
            AddEnemy(info, "brute", 44, 98); AddEnemy(info, "brute", 58, 96); AddEnemy(info, "warrior", 37, 94, 1.1f); AddEnemy(info, "shield_bearer", 63, 102, 1.1f);   // [Inimigos] escudeiro (era: guerreiro)
            AddEnemy(info, "mage", 50, 107); AddEnemy(info, "rogue", 34, 104); AddEnemy(info, "rogue", 66, 90);
            AddEnemy(info, "charger", 52, 92); AddEnemy(info, "healer", 40, 104);   // [Inimigos] aríete e curandeiro
            // Necromante
            AddEnemy(info, "necro", -10, 104); AddEnemy(info, "minion", -15, 96); AddEnemy(info, "minion", -5, 96); AddEnemy(info, "mage", -10, 112);
            info.hasSanctuary = true;
            info.sanctuary = V(-10, 140);
            Reserve(info.sanctuary, 3f);

            // ---- santuário: ouro, lanternas e brilho
            D.PointLight(gLuz, info.sanctuary + new Vector3(0, 3f, 0), new Color(0.6f, 0.85f, 1f), 12f, 2.6f, false, false);
            D.Motes(gProps, info.sanctuary + new Vector3(0, 1.5f, 0), new Vector3(8f, 3f, 8f), new Color(0.6f, 0.9f, 1f, 1f), 30, 0.08f);
            Prop("Gold_Nuggets", V(-19, 144), 30f, 0.5f);
            Prop("Gold_Bars_Stack_Small", V(-1, 144), -20f, 0.6f, true);
            D.StandingTorch(gProps, gLuz, V(-15, 135), 2.2f, 7f);
            D.StandingTorch(gProps, gLuz, V(-5, 135), 2.2f, 7f);

            // ---- entulho, ossos (armas de esqueleto caídas), barris e ruínas
            string[] bones = { "Skeleton_Blade", "Skeleton_Shield_Small_A", "Skeleton_Axe", "Skeleton_Arrow", "Skeleton_Shield_Large_A" };
            string[] rubble = { "rock_single_A", "rock_single_C", "Stone_Chunks_Large" };
            var halls = new[] { new RectInt(-4, 9, 8, 6), new RectInt(10, 8, 7, 9), new RectInt(8, 22, 10, 6), new RectInt(-6, 21, 8, 9), new RectInt(-2, 0, 5, 4) };
            foreach (var hr in halls)
            {
                int n = hr.width * hr.height / 4;
                for (int k = 0; k < n; k++)
                {
                    int ci = Ri(hr.xMin, hr.xMax), cj = Ri(hr.yMin, hr.yMax);
                    if (!IsFloor(ci, cj) || IsLava(ci, cj)) continue;
                    Vector3 p = Cell(ci, cj) + new Vector3(Rf(-1.6f, 1.6f), 0, Rf(-1.6f, 1.6f));
                    if (!Free(p, 0.7f)) continue;
                    float roll = Rf(0, 1);
                    if (roll < 0.45f) D.SpawnLying(Pick(bones), gProps, p, Rf(0, 360), Rf(0.6f, 1.1f));
                    else if (roll < 0.8f) D.SpawnSized(Pick(rubble), gProps, p, Rf(0, 360), Rf(0.2f, 0.5f));
                    else D.SpawnSized(Pick(ROCKS_SMALL), gProps, p, Rf(0, 360), Rf(0.25f, 0.45f));
                }
            }
            // cantos com barris/caixas
            // (x, z, direção para dentro da sala x, z)
            foreach (var q in new[] { new Vector4(-16, 37, 1, 1), new Vector4(12, 37, -1, 1), new Vector4(39.5f, 31.5f, 1, 1), new Vector4(64.5f, 64.5f, -1, -1),
                                      new Vector4(31.5f, 108.5f, 1, -1), new Vector4(68.5f, 87.5f, -1, 1), new Vector4(-24.5f, 83.5f, 1, 1), new Vector4(-20.5f, 131.5f, 1, 1) })
            {
                Vector3 p = V(q.x, q.y);
                Vector3 inward = new Vector3(q.z, 0, q.w);
                Prop("barrel", p, Rf(0, 360), H_BARREL, true);
                Prop(Chance(0.5f) ? "crate_A_big" : "crate_long_A", p + new Vector3(inward.x * 1.3f, 0, 0), Rf(0, 360), H_CRATE, true);
                Prop("rope_bundle_A", p + new Vector3(0, 0, inward.z * 1.2f), Rf(0, 360), 0.15f);
            }
            // ruínas desabadas
            foreach (var p in new[] { V(65, 107), V(34, 89) })
            {
                var r = D.SpawnSized("building_destroyed", gCon, p, Rf(0, 360), 2.8f, 0f, true);
                D.AddCollider(r, 0.75f, 2f);
                D.MarkOccluder(r);
            }
            return info;
        }

        // =================================================================================================
        //  [Quebraveis] barris, caixas, vasos e barris explosivos espalhados pelos andares
        // =================================================================================================
        static string PickBreakable()
        {
            float r = Rf(0f, 1f);
            if (r < 0.18f) return "explosive";
            if (r < 0.45f) return "barrel";
            if (r < 0.68f) return "crate";
            if (r < 0.85f) return "crate_small";
            return "vase";
        }

        /// <summary>Espalha quebráveis em pontos livres (chamado depois de montar o andar; usa o rng do mapa).</summary>
        static LevelInfo ScatterBreakables(LevelInfo info)
        {
            if (info == null || info.kind != "dungeon") return info;
            var spots = new List<Vector3>();
            if (info.id == "tower_f1")
            {
                // clareiras 1..4 (lacaios até a arena do chefe)
                for (int i = 1; i <= 4 && i < F1_ROOMS.Length; i++)
                {
                    var room = F1_ROOMS[i];
                    int want = i == 4 ? 5 : 4;
                    for (int k = 0, tries = 0; k < want && tries < 30; tries++)
                    {
                        float a = Rf(0, Mathf.PI * 2f), rad = Rf(2.5f, room.r - 1.5f);
                        Vector3 p = room.c + new Vector3(Mathf.Cos(a) * rad, 0, Mathf.Sin(a) * rad);
                        if (!Free(p, 1.1f)) continue;
                        spots.Add(p); Reserve(p, 0.9f); k++;
                        // às vezes um segundo objeto colado (pilha de caixas/barris)
                        if (Chance(0.35f))
                        {
                            Vector3 q = p + new Vector3(Rf(-1f, 1f), 0, Rf(-1f, 1f)).normalized * 1.05f;
                            if (Free(q, 0.6f)) { spots.Add(q); Reserve(q, 0.7f); }
                        }
                    }
                }
            }
            else if (info.id == "tower_f2" && carved != null)
            {
                var halls = new[] { new RectInt(-4, 9, 8, 6), new RectInt(10, 8, 7, 9), new RectInt(8, 22, 10, 6), new RectInt(-6, 21, 8, 9), new RectInt(-2, 0, 5, 4) };
                foreach (var hr in halls)
                {
                    int want = Mathf.Clamp(hr.width * hr.height / 10, 2, 6);
                    for (int k = 0, tries = 0; k < want && tries < 40; tries++)
                    {
                        int ci = Ri(hr.xMin, hr.xMax), cj = Ri(hr.yMin, hr.yMax);
                        if (!IsFloor(ci, cj) || IsLava(ci, cj)) continue;
                        // longe das paredes: as 4 vizinhas também são chão
                        if (!IsFloor(ci + 1, cj) || !IsFloor(ci - 1, cj) || !IsFloor(ci, cj + 1) || !IsFloor(ci, cj - 1)) continue;
                        Vector3 p = Cell(ci, cj) + new Vector3(Rf(-1.4f, 1.4f), 0, Rf(-1.4f, 1.4f));
                        if (!Free(p, 1.1f)) continue;
                        spots.Add(p); Reserve(p, 0.9f); k++;
                    }
                }
            }
            float floorY = info.id == "tower_f2" ? 0.02f : 0f;   // topo dos ladrilhos das ruínas
            foreach (var p in spots)
                Breakable.Spawn(PickBreakable(), new Vector3(p.x, floorY, p.z), gProps, Rf(0, 360));
            return info;
        }
    }
}
