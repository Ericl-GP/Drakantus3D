using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace Drakantus.EditorTools
{
    /// <summary>Opções do Criador de Andares (também ficam gravadas no FloorRoot para "Regerar").</summary>
    [Serializable]
    public class FloorSettings
    {
        // andar
        public int floor = 3;
        public string id = "";
        public string floorName = "Salões Esquecidos";
        public string subtitle = "";
        public int difficulty = 3;
        public int recLevel = 6;
        public string next = "";
        // tamanho
        public int sizePreset = 1;
        public int width = 80, length = 120;
        public int rooms = 8;
        // bioma
        public string biome = "masmorra";
        public string biome2 = "";
        public float mix = 0.4f;
        // estrutura
        public int levels;
        public bool wideCorridors;
        public bool secretRooms = true;
        public bool lockedDoor = true;
        public bool arenaDoors = true;
        // conteúdo
        public float enemyDensity = 1f;
        public int elitePercent = 15;
        public int miniBosses = 1;
        public string boss = "";          // "" = aleatório do bioma, "-" = sem chefe
        public int hiddenTraps = 4;
        public int visibleTraps = 4;
        public bool poisonFog = true;
        public int commonChests = 3;
        public int rareChests = 1;
        public bool sanctuary = true;
        // outros
        public int seed = 12345;
        public string testClass = "";

        public string FloorId => string.IsNullOrEmpty(id) ? "tower_f" + floor : id;
        public string SceneName => "Andar_" + floor.ToString("00");

        public static readonly string[] SizeNames = { "Pequeno", "Médio", "Grande", "Enorme", "Personalizado" };
        static readonly int[] PW = { 60, 80, 100, 128 }, PL = { 88, 120, 160, 200 }, PR = { 5, 8, 12, 18 };

        public void ApplyPreset()
        {
            if (sizePreset < 0 || sizePreset > 3) return;
            width = PW[sizePreset];
            length = PL[sizePreset];
            rooms = PR[sizePreset];
        }

        public FloorSettings Clone() => JsonUtility.FromJson<FloorSettings>(JsonUtility.ToJson(this));
    }

    /// <summary>
    /// Gera um andar completo: grade de salas retangulares ligadas por corredores (caminho garantido da
    /// entrada ao chefe), salas laterais/secreta, arena do chefe seguida do santuário, níveis com rampas,
    /// paredes (as viradas para a câmera ficam baixas), portas + alavanca, armadilhas, névoa, baús, luzes,
    /// decoração do bioma e partículas. Mesma semente = mesmo andar.
    /// </summary>
    public static class FloorGenerator
    {
        const float T = FloorKit.TILE;
        const float LEVEL_H = 1.2f;

        class Room
        {
            public int idx;
            public RectInt r;
            public string role = "normal";   // entrada, normal, lateral, minichefe, chefe, santuario, secreta
            public int path = -1;
            public float h;
            public BiomeDef b;
            public Vector2Int slot;
            public Room parent;
            public readonly List<Corr> corrs = new List<Corr>();
        }

        class Corr
        {
            public Room a, b;
            public string kind;               // path, side, secret, sanct
            public bool narrow;
            public readonly List<Vector2Int> seq = new List<Vector2Int>();
            public readonly List<int> outside = new List<int>();   // índices de seq fora de salas
        }

        static Random rng;
        static FloorSettings S;
        static BiomeDef B1, B2;
        static int GW, GH, cols, rows, sw, sh, slotTop;
        static int[,] kind;          // 0 vazio, 1 sala, 2 corredor
        static int[,] roomOf;
        static float[,] hIn, hOut;
        static Vector2Int[,] cdir;
        static bool[,] hSet, used, lavaCell;
        static List<Room> rooms;
        static List<Corr> corrs;
        static List<Room> path;
        static readonly List<Vector3> hard = new List<Vector3>();   // (x, raio, z) — objetos importantes
        static readonly List<Vector3> lanes = new List<Vector3>();  // caminhos livres dentro das salas
        static int lights, shadowLights, enemyCount, eliteCount, trapCount, chestCount;
        static StringBuilder report;
        static Transform gChao, gPar, gDeco, gLuz, gArm, gPortas, gFx, gIni, gMini, gChefe, gBau, gEsc, gSant, gGat, gCol;

        public static string LastReport = "";

        // ================================================================== utilidades
        static float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static int Ri(int a, int bExcl) => bExcl <= a ? a : rng.Next(a, bExcl);
        static bool Chance(float p) => rng.NextDouble() < p;
        static TT Pick<TT>(IList<TT> l) => l[rng.Next(l.Count)];

        static Vector3 C(int i, int j) => new Vector3((i - GW * 0.5f + 0.5f) * T, 0f, (j + 0.5f) * T);
        static Vector3 C(Vector2Int v) => C(v.x, v.y);
        static bool In(int i, int j) => i >= 0 && j >= 0 && i < GW && j < GH;
        static bool IsFloor(int i, int j) => In(i, j) && kind[i, j] != 0;
        static float Top(int i, int j) => Mathf.Max(hIn[i, j], hOut[i, j]);
        static bool Flat(Vector2Int c) => Mathf.Abs(hIn[c.x, c.y] - hOut[c.x, c.y]) < 0.01f;
        static Vector2Int CenterCell(Room r) => new Vector2Int(r.r.xMin + r.r.width / 2, r.r.yMin + r.r.height / 2);
        static Vector3 RoomCenter(Room r) => new Vector3(((r.r.xMin + r.r.xMax) * 0.5f - GW * 0.5f) * T, r.h, (r.r.yMin + r.r.yMax) * 0.5f * T);
        static float MinX(Room r) => (r.r.xMin - GW * 0.5f) * T;
        static float MaxX(Room r) => (r.r.xMax - GW * 0.5f) * T;
        static float MinZ(Room r) => r.r.yMin * T;
        static float MaxZ(Room r) => r.r.yMax * T;
        static Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / T + GW * 0.5f), Mathf.FloorToInt(p.z / T));

        static void Reserve(Vector3 p, float r) => hard.Add(new Vector3(p.x, r, p.z));

        static bool Free(Vector3 p, float r, bool avoidLanes)
        {
            foreach (var q in hard)
            {
                float dx = p.x - q.x, dz = p.z - q.z, rr = r + q.y;
                if (dx * dx + dz * dz < rr * rr) return false;
            }
            if (avoidLanes)
                foreach (var q in lanes)
                {
                    float dx = p.x - q.x, dz = p.z - q.z, rr = r + q.y;
                    if (dx * dx + dz * dz < rr * rr) return false;
                }
            var c = CellOf(p);
            if (In(c.x, c.y) && lavaCell[c.x, c.y]) return false;
            return true;
        }

        static bool FreePoint(Room r, float margin, float rad, bool avoidLanes, out Vector3 p)
        {
            for (int k = 0; k < 30; k++)
            {
                float x0 = MinX(r) + margin, x1 = MaxX(r) - margin, z0 = MinZ(r) + margin, z1 = MaxZ(r) - margin;
                if (x1 <= x0 || z1 <= z0) break;
                p = new Vector3(Rf(x0, x1), r.h, Rf(z0, z1));
                if (Free(p, rad, avoidLanes)) return true;
            }
            p = RoomCenter(r);
            return false;
        }

        static Color Hex(string h, Color fb) => FloorKit.Hex(h, fb);

        /// <summary>Retângulo (centro, largura em X, profundidade em Z) livre de reservas (e trilhas).</summary>
        static bool FreeRect(Vector3 center, float w, float d, bool avoidLanes)
        {
            for (float x = -w * 0.5f; x <= w * 0.5f + 0.01f; x += 1.5f)
                for (float z = -d * 0.5f; z <= d * 0.5f + 0.01f; z += 1.5f)
                    if (!Free(center + new Vector3(x, 0f, z), 0.9f, avoidLanes)) return false;
            return true;
        }

        static void ReserveRect(Vector3 center, float w, float d)
        {
            for (float x = -w * 0.5f; x <= w * 0.5f + 0.01f; x += 1.5f)
                for (float z = -d * 0.5f; z <= d * 0.5f + 0.01f; z += 1.5f)
                    Reserve(center + new Vector3(x, 0f, z), 1.1f);
        }


        // ================================================================== entrada
        public static GameObject Generate(FloorSettings s)
        {
            S = s;
            rng = new Random(s.seed);
            FloorPieces.rng = new Random(s.seed * 7 + 13);
            FloorKit.count = 0;
            FloorKit.ResetCaches();
            Biomes.Reload();
            BossProfiles.Reload();
            GameData.Load();
            report = new StringBuilder();
            hard.Clear(); lanes.Clear();
            lights = shadowLights = enemyCount = eliteCount = trapCount = chestCount = 0;
            rareFromArenas = 0;
            secretChest = false;

            var all = Biomes.All();
            B1 = Biomes.Get(s.biome) ?? (all.Count > 0 ? all[0] : new BiomeDef { id = "padrao", name = "Padrão" });
            B2 = string.IsNullOrEmpty(s.biome2) || s.biome2 == B1.id ? null : Biomes.Get(s.biome2);
            FloorPieces.biome = B1;

            int topReserve = s.sanctuary ? 6 : 0;
            GW = Mathf.Clamp(Mathf.RoundToInt(s.width / T), 12, 64);
            GH = Mathf.Clamp(Mathf.RoundToInt(s.length / T), 12 + topReserve, 80);

            kind = new int[GW, GH];
            roomOf = new int[GW, GH];
            hIn = new float[GW, GH];
            hOut = new float[GW, GH];
            cdir = new Vector2Int[GW, GH];
            hSet = new bool[GW, GH];
            used = new bool[GW, GH];
            lavaCell = new bool[GW, GH];
            for (int i = 0; i < GW; i++) for (int j = 0; j < GH; j++) roomOf[i, j] = -1;
            rooms = new List<Room>();
            corrs = new List<Corr>();
            path = new List<Room>();

            var go = new GameObject(s.SceneName);
            var fr = go.AddComponent<FloorRoot>();
            FillRoot(fr);
            var t = go.transform;
            gChao = FloorKit.Group(t, "Chao");
            gPar = FloorKit.Group(t, "Paredes");
            gDeco = FloorKit.Group(t, "Decoracao");
            gLuz = FloorKit.Group(t, "Luzes");
            gArm = FloorKit.Group(t, "Armadilhas");
            gPortas = FloorKit.Group(t, "Portas_Alavancas");
            gFx = FloorKit.Group(t, "Efeitos");
            gIni = FloorKit.Group(t, "Inimigos");
            gMini = FloorKit.Group(t, "MiniChefes");
            gChefe = FloorKit.Group(t, "Chefes");
            gBau = FloorKit.Group(t, "Baus");
            gEsc = FloorKit.Group(t, "Escadas");
            gSant = FloorKit.Group(t, "Santuario_Portais");
            gGat = FloorKit.Group(t, "Gatilhos");
            gCol = FloorKit.Group(t, "Colisores");

            Layout(topReserve);
            CarveAll();
            Heights();
            BuildGround();
            BuildFloor();
            BuildWalls();
            Entrance();
            SanctuaryAndBoss();
            MiniBosses();
            Platforms();
            Enemies();
            Doors();
            LeverDoor();
            Secret();
            Traps();
            Fog();
            Chests();
            Breakables();   // [Quebraveis]
            Lights();
            Decorate();
            Backdrop();
            Ambient();

            fr.boundsCenter = new Vector3(0f, 3f, GH * T * 0.5f);
            fr.boundsSize = new Vector3(GW * T, 16f, GH * T);

            report.Insert(0, string.Format("Andar {0} ({1}) · bioma {2}{3} · {4}x{5} m · semente {6}\n" +
                "Salas: {7} · Inimigos: {8} (elites {9}) · Armadilhas: {10} · Baús: {11} · Luzes: {12} · Objetos: {13}\n",
                s.floor, s.FloorId, B1.name, B2 != null ? " + " + B2.name : "", GW * T, GH * T, s.seed,
                rooms.Count, enemyCount, eliteCount, trapCount, chestCount, lights, FloorKit.count));
            if (FloorKit.Full) report.AppendLine("Aviso: limite de ~" + FloorKit.limit + " objetos atingido; parte da decoração foi omitida.");
            LastReport = report.ToString();
            return go;
        }

        static void FillRoot(FloorRoot fr)
        {
            fr.floorId = S.FloorId;
            fr.floor = S.floor;
            fr.floorName = string.IsNullOrEmpty(S.floorName) ? "Andar " + S.floor.ToString("00") : S.floorName;
            fr.title = "Torre de Aster";
            fr.subtitle = string.IsNullOrEmpty(S.subtitle) ? "Andar " + S.floor.ToString("00") + " · " + fr.floorName : S.subtitle;
            fr.next = S.next ?? "";
            fr.difficulty = Mathf.Clamp(S.difficulty, 1, 10);
            fr.recommendedLevel = Mathf.Max(1, S.recLevel);
            fr.biome = B1.id;
            fr.biome2 = B2 != null ? B2.id : "";
            fr.music = string.IsNullOrEmpty(B1.music) ? "dungeon" : B1.music;
            fr.ambient = Hex(B1.ambient, fr.ambient);
            fr.sun = Hex(B1.sun, fr.sun);
            fr.sunIntensity = B1.sunIntensity;
            fr.sunPitch = B1.sunPitch;
            fr.sunYaw = B1.sunYaw;
            fr.dark = B1.dark;
            fr.fog = Hex(B1.fog, fr.fog);
            fr.fogDensity = B1.fogDensity;
            fr.enemyTint = Hex(B1.enemyTint, Color.white);
            fr.seed = S.seed;
            fr.generatorSettings = JsonUtility.ToJson(S);
        }

        // ================================================================== planta: salas em grade + caminho principal
        static void Layout(int topReserve)
        {
            int innerW = GW - 2, innerH = GH - 2 - topReserve;
            int N = Mathf.Clamp(S.rooms, 3, 40);
            int maxCols = Mathf.Max(1, innerW / 5), maxRows = Mathf.Max(2, innerH / 5);
            cols = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(N * innerW / (float)Mathf.Max(1, innerH))), 1, maxCols);
            rows = Mathf.Clamp(Mathf.CeilToInt(N / (float)cols), 2, maxRows);
            while (cols * rows < N && cols < maxCols) cols++;
            while (cols * rows < N && rows < maxRows) rows++;
            if (cols * rows < N)
            {
                report.AppendLine("Aviso: o tamanho comporta só " + (cols * rows) + " salas (pedido " + N + "). Aumente o andar.");
                N = cols * rows;
            }
            sw = innerW / cols;
            sh = innerH / rows;
            slotTop = 1 + rows * sh;

            // caminho principal (de baixo para cima, com desvios laterais)
            int Lmain = Mathf.Clamp(Mathf.RoundToInt(N * 0.7f), rows, N);
            int extra = Lmain - rows;
            var slotUsed = new bool[cols, rows];
            var pathSlots = new List<Vector2Int>();
            int c = Ri(0, cols);
            pathSlots.Add(new Vector2Int(c, 0));
            slotUsed[c, 0] = true;
            for (int r = 0; r < rows; r++)
            {
                int want = 0;
                if (extra > 0) want = r == rows - 1 ? Mathf.Min(extra, cols - 1) : Ri(0, Mathf.Min(extra, cols - 1) + 1);
                int dir = c == 0 ? 1 : c == cols - 1 ? -1 : (Chance(0.5f) ? -1 : 1);
                for (int m = 0; m < want; m++)
                {
                    int nc = c + dir;
                    if (nc < 0 || nc >= cols || slotUsed[nc, r]) break;
                    c = nc;
                    slotUsed[c, r] = true;
                    pathSlots.Add(new Vector2Int(c, r));
                    extra--;
                }
                if (r < rows - 1)
                {
                    pathSlots.Add(new Vector2Int(c, r + 1));
                    slotUsed[c, r + 1] = true;
                }
            }

            // papéis no caminho
            int L = pathSlots.Count;
            var roles = new string[L];
            for (int k = 0; k < L; k++) roles[k] = "normal";
            roles[0] = "entrada";
            bool hasBoss = S.boss != "-";
            if (hasBoss) roles[L - 1] = "chefe";
            int mb = Mathf.Clamp(S.miniBosses, 0, Mathf.Max(0, L - 3));
            for (int k = 0; k < mb; k++)
            {
                int idx = Mathf.Clamp(Mathf.RoundToInt((k + 1) * (L - 1) / (float)(mb + 1)), 1, L - 2);
                for (int tries = 0; tries < L && roles[idx] != "normal"; tries++) idx = 1 + (idx % Mathf.Max(1, L - 2));
                if (roles[idx] == "normal") roles[idx] = "minichefe";
            }
            if (S.miniBosses > mb) report.AppendLine("Aviso: só coube(ram) " + mb + " mini-chefe(s) no caminho.");

            for (int k = 0; k < L; k++)
            {
                var room = NewRoom(pathSlots[k], roles[k]);
                room.path = k;
                path.Add(room);
            }

            // salas laterais
            int sideN = N - L;
            for (int k = 0; k < sideN; k++)
            {
                if (!AddSide(slotUsed, "lateral")) { report.AppendLine("Aviso: sem espaço para mais salas laterais."); break; }
            }
            // sala secreta (extra)
            if (S.secretRooms && !AddSide(slotUsed, "secreta")) report.AppendLine("Aviso: sem espaço livre para a sala secreta.");

            // santuário (faixa de cima)
            if (S.sanctuary)
            {
                Room last = path[path.Count - 1];
                int w = Mathf.Min(4, GW - 4);
                int cx = CenterCell(last).x;
                int x0 = Mathf.Clamp(cx - w / 2, 1, GW - 1 - w);
                int z0 = slotTop + 1;
                int h = Mathf.Clamp(GH - 2 - z0, 3, 4);
                var room = new Room { idx = rooms.Count, r = new RectInt(x0, z0, w, h), role = "santuario", slot = new Vector2Int(last.slot.x, rows) };
                rooms.Add(room);
                room.parent = last;
            }
        }

        static Room NewRoom(Vector2Int slot, string role)
        {
            int x0 = 1 + slot.x * sw, z0 = 1 + slot.y * sh;
            int aw = Mathf.Max(3, sw - 2), ah = Mathf.Max(3, sh - 2);
            int w, h;
            switch (role)
            {
                case "chefe": w = aw; h = ah; break;
                case "minichefe": w = Mathf.Max(3, Mathf.RoundToInt(aw * 0.85f)); h = Mathf.Max(3, Mathf.RoundToInt(ah * 0.85f)); break;
                case "secreta": w = Mathf.Min(aw, 3); h = Mathf.Min(ah, 3); break;
                case "lateral": w = Ri(3, Mathf.Max(3, Mathf.RoundToInt(aw * 0.75f)) + 1); h = Ri(3, Mathf.Max(3, Mathf.RoundToInt(ah * 0.75f)) + 1); break;
                case "entrada": w = Ri(Mathf.Max(3, aw / 2), aw + 1); h = Ri(Mathf.Max(3, ah / 2), ah + 1); break;
                default: w = Ri(Mathf.Max(3, Mathf.RoundToInt(aw * 0.55f)), aw + 1); h = Ri(Mathf.Max(3, Mathf.RoundToInt(ah * 0.55f)), ah + 1); break;
            }
            w = Mathf.Min(w, aw); h = Mathf.Min(h, ah);
            int ox = x0 + 1 + Ri(0, aw - w + 1);
            int oz = z0 + 1 + Ri(0, ah - h + 1);
            var room = new Room { idx = rooms.Count, r = new RectInt(ox, oz, w, h), role = role, slot = slot };
            rooms.Add(room);
            return room;
        }

        static bool AddSide(bool[,] slotUsed, string role)
        {
            var cands = new List<KeyValuePair<Vector2Int, Room>>();
            Vector2Int[] dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            foreach (var pr in path)
            {
                if (pr.role == "chefe") continue;
                if (role == "secreta" && (pr.path < 1 || pr.path >= path.Count - 1)) continue;
                foreach (var d in dirs)
                {
                    var s2 = pr.slot + d;
                    if (s2.x < 0 || s2.y < 0 || s2.x >= cols || s2.y >= rows || slotUsed[s2.x, s2.y]) continue;
                    cands.Add(new KeyValuePair<Vector2Int, Room>(s2, pr));
                }
            }
            if (cands.Count == 0) return false;
            var pick = Pick(cands);
            // prefere o pai mais cedo no caminho
            foreach (var cnd in cands)
                if (cnd.Key == pick.Key && cnd.Value.path < pick.Value.path) pick = cnd;
            slotUsed[pick.Key.x, pick.Key.y] = true;
            var room = NewRoom(pick.Key, role);
            room.parent = pick.Value;
            return true;
        }

        // ================================================================== escavar salas e corredores
        static void CarveAll()
        {
            foreach (var r in rooms)
                for (int i = r.r.xMin; i < r.r.xMax; i++)
                    for (int j = r.r.yMin; j < r.r.yMax; j++)
                    {
                        kind[i, j] = 1;
                        roomOf[i, j] = r.idx;
                    }
            for (int k = 1; k < path.Count; k++) Connect(path[k - 1], path[k], "path", !S.wideCorridors);
            foreach (var r in rooms)
            {
                if (r.role == "lateral") Connect(r.parent, r, "side", !S.wideCorridors);
                else if (r.role == "secreta") Connect(r.parent, r, "secret", true);
                else if (r.role == "santuario") Connect(r.parent, r, "sanct", !S.wideCorridors);
            }
        }

        static void Walk(List<Vector2Int> list, Vector2Int from, Vector2Int to)
        {
            Vector2Int p = from;
            if (list.Count == 0 || list[list.Count - 1] != p) list.Add(p);
            int guard = 0;
            while (p != to && guard++ < 500)
            {
                if (p.x != to.x) p.x += Math.Sign(to.x - p.x);
                else p.y += Math.Sign(to.y - p.y);
                list.Add(p);
            }
        }

        static void Connect(Room a, Room b, string ckind, bool narrow)
        {
            var c = new Corr { a = a, b = b, kind = ckind, narrow = narrow };
            Vector2Int ca = CenterCell(a), cb = CenterCell(b);
            bool vert = a.r.yMax <= b.r.yMin || b.r.yMax <= a.r.yMin;
            if (vert)
            {
                int mid = a.r.yMax <= b.r.yMin ? (a.r.yMax + b.r.yMin) / 2 : (b.r.yMax + a.r.yMin) / 2;
                Walk(c.seq, ca, new Vector2Int(ca.x, mid));
                Walk(c.seq, new Vector2Int(ca.x, mid), new Vector2Int(cb.x, mid));
                Walk(c.seq, new Vector2Int(cb.x, mid), cb);
            }
            else
            {
                int mid = a.r.xMax <= b.r.xMin ? (a.r.xMax + b.r.xMin) / 2 : (b.r.xMax + a.r.xMin) / 2;
                Walk(c.seq, ca, new Vector2Int(mid, ca.y));
                Walk(c.seq, new Vector2Int(mid, ca.y), new Vector2Int(mid, cb.y));
                Walk(c.seq, new Vector2Int(mid, cb.y), cb);
            }
            for (int k = 0; k < c.seq.Count; k++)
            {
                var cell = c.seq[k];
                if (kind[cell.x, cell.y] == 0) kind[cell.x, cell.y] = 2;
                if (roomOf[cell.x, cell.y] < 0) c.outside.Add(k);
                if (!narrow)
                {
                    Vector2Int din = k > 0 ? c.seq[k] - c.seq[k - 1] : (k + 1 < c.seq.Count ? c.seq[k + 1] - c.seq[k] : Vector2Int.right);
                    Vector2Int dout = k + 1 < c.seq.Count ? c.seq[k + 1] - c.seq[k] : din;
                    Vector2Int pi = Perp(din), po = Perp(dout);
                    CarveSib(cell + pi);
                    if (po != pi) { CarveSib(cell + po); CarveSib(cell + pi + po); }
                }
            }
            a.corrs.Add(c);
            b.corrs.Add(c);
            corrs.Add(c);
        }

        static Vector2Int Perp(Vector2Int d) => new Vector2Int(Mathf.Abs(d.y), Mathf.Abs(d.x));

        static void CarveSib(Vector2Int c)
        {
            if (c.x < 1 || c.y < 1 || c.x > GW - 2 || c.y > GH - 2) return;
            if (kind[c.x, c.y] == 0) kind[c.x, c.y] = 2;
        }

        // ================================================================== alturas (níveis e rampas)
        static void Heights()
        {
            float maxH = Mathf.Clamp(S.levels, 0, 3) * LEVEL_H;
            for (int k = 0; k < path.Count; k++)
            {
                var r = path[k];
                if (k == 0 || S.levels <= 0) { r.h = 0f; continue; }
                float prev = path[k - 1].h;
                double roll = rng.NextDouble();
                float step = roll < 0.4 ? LEVEL_H : roll < 0.62 ? -LEVEL_H : 0f;
                if (r.role == "chefe" && step < 0f) step = 0f;
                r.h = Mathf.Clamp(prev + step, 0f, maxH);
            }
            foreach (var r in rooms) if (r.path < 0) r.h = r.parent != null ? r.parent.h : 0f;
            foreach (var r in rooms)
            {
                r.b = (B2 != null && r.role != "chefe" && r.role != "entrada" && Chance(Mathf.Clamp01(S.mix))) ? B2 : B1;
                for (int i = r.r.xMin; i < r.r.xMax; i++)
                    for (int j = r.r.yMin; j < r.r.yMax; j++) { hIn[i, j] = hOut[i, j] = r.h; hSet[i, j] = true; }
            }
            foreach (var c in corrs)
            {
                int n = c.outside.Count;
                if (n == 0) continue;
                float ha = c.a.h, hb = c.b.h;
                var w = new float[n];
                float sum = 0f;
                for (int k = 0; k < n; k++)
                {
                    int idx = c.outside[k];
                    Vector2Int din = idx > 0 ? c.seq[idx] - c.seq[idx - 1] : c.seq[idx + 1] - c.seq[idx];
                    Vector2Int dout = idx + 1 < c.seq.Count ? c.seq[idx + 1] - c.seq[idx] : din;
                    w[k] = din == dout ? 1f : 0f;
                    if (n >= 3 && (k == 0 || k == n - 1)) w[k] = 0f;
                    sum += w[k];
                }
                if (sum <= 0f) { for (int k = 0; k < n; k++) w[k] = 1f; sum = n; }
                float acc = 0f;
                for (int k = 0; k < n; k++)
                {
                    int idx = c.outside[k];
                    var cell = c.seq[idx];
                    float h0 = ha + (hb - ha) * acc / sum;
                    acc += w[k];
                    float h1 = ha + (hb - ha) * acc / sum;
                    Vector2Int din = idx > 0 ? c.seq[idx] - c.seq[idx - 1] : c.seq[idx + 1] - c.seq[idx];
                    Vector2Int dout = idx + 1 < c.seq.Count ? c.seq[idx + 1] - c.seq[idx] : din;
                    SetH(cell, h0, h1, dout);
                    if (!c.narrow)
                    {
                        Vector2Int pi = Perp(din), po = Perp(dout);
                        if (pi == po) SetH(cell + pi, h0, h1, dout);
                        else { float hm = Mathf.Max(h0, h1); SetH(cell + pi, hm, hm, dout); SetH(cell + po, hm, hm, dout); SetH(cell + pi + po, hm, hm, dout); }
                    }
                }
            }
            // células de corredor que sobraram sem altura (laterais de corredores largos)
            for (int i = 0; i < GW; i++)
                for (int j = 0; j < GH; j++)
                    if (kind[i, j] != 0 && !hSet[i, j])
                    {
                        float best = 0f;
                        for (int di = -1; di <= 1; di++)
                            for (int dj = -1; dj <= 1; dj++)
                                if (In(i + di, j + dj) && hSet[i + di, j + dj]) best = Mathf.Max(best, Top(i + di, j + dj));
                        hIn[i, j] = hOut[i, j] = best;
                        hSet[i, j] = true;
                    }
        }

        static void SetH(Vector2Int c, float a, float b, Vector2Int d)
        {
            if (!In(c.x, c.y) || kind[c.x, c.y] == 0 || hSet[c.x, c.y]) return;
            hIn[c.x, c.y] = a;
            hOut[c.x, c.y] = b;
            cdir[c.x, c.y] = d;
            hSet[c.x, c.y] = true;
        }

        // ================================================================== chão e paredes
        static void BuildGround()
        {
            Vector3 center = new Vector3(0f, 0f, GH * T * 0.5f);
            Vector3 size = new Vector3(GW * T + 48f, 0.5f, GH * T + 48f);
            FloorKit.Cube(gChao, "Chao_Fundo", center + Vector3.down * 0.3f, size, Quaternion.identity, FloorKit.Mat(Hex(B1.baseColor, new Color(0.12f, 0.12f, 0.14f)), 0.05f), false);
            FloorKit.ColliderBox(gCol, center + Vector3.down * 0.5f, new Vector3(size.x, 1f, size.z), 0f, "Colisor_Chao");
        }

        static void BuildFloor()
        {
            foreach (var r in rooms)
            {
                if (r.h > 0.05f)
                {
                    Vector3 c = RoomCenter(r);
                    FloorKit.ColliderBox(gCol, new Vector3(c.x, (r.h - 0.5f) * 0.5f, c.z), new Vector3(r.r.width * T, r.h + 0.5f, r.r.height * T), 0f, "Colisor_Sala");
                }
            }
            for (int i = 0; i < GW; i++)
                for (int j = 0; j < GH; j++)
                {
                    if (kind[i, j] == 0) continue;
                    Vector3 c = C(i, j);
                    int ro = roomOf[i, j];
                    FloorPieces.biome = ro >= 0 ? rooms[ro].b : B1;
                    bool alt = Chance(FloorPieces.biome.floorAltChance);
                    float a = hIn[i, j], b = hOut[i, j];
                    if (Mathf.Abs(a - b) > 0.01f && cdir[i, j] != Vector2Int.zero)
                    {
                        var fb = FloorPieces.biome;
                        FloorKit.RampBox(fb.floorBlock, gChao, c, cdir[i, j], a, b, T, T, new Color(0.45f, 0.45f, 0.48f), Hex(fb.floorTint, Color.white));
                        float lo = Mathf.Min(a, b);
                        if (lo > 0.05f) FloorPieces.FloorTile(gChao, c, lo - 0.02f, T, alt);
                    }
                    else
                    {
                        FloorPieces.FloorTile(gChao, c, a, T, alt);
                        if (ro < 0 && a > 0.05f)
                            FloorKit.ColliderBox(gCol, new Vector3(c.x, (a - 0.5f) * 0.5f, c.z), new Vector3(T, a + 0.5f, T), 0f, "Colisor_Corredor");
                    }
                }
            FloorPieces.biome = B1;
        }

        static void BuildWalls()
        {
            float wallH = Mathf.Max(2.2f, B1.wallHeight);
            DecoDef[] bigRocks = Filter(B1.rocks, true);
            DecoDef[] trees = B1.trees != null && B1.trees.Length > 0 ? B1.trees : B1.backdrop;
            for (int i = 0; i < GW; i++)
                for (int j = 0; j < GH; j++)
                {
                    if (kind[i, j] != 0) continue;
                    float nh = -1f;
                    for (int di = -1; di <= 1; di++)
                        for (int dj = -1; dj <= 1; dj++)
                            if (IsFloor(i + di, j + dj)) nh = Mathf.Max(nh, Top(i + di, j + dj));
                    if (nh < 0f) continue;
                    bool front = (IsFloor(i + 1, j) || IsFloor(i, j + 1) || IsFloor(i + 1, j + 1)) && !IsFloor(i - 1, j) && !IsFloor(i, j - 1) && !IsFloor(i - 1, j - 1);
                    Vector3 c = C(i, j);
                    switch (B1.wallStyle)
                    {
                        case "trees":
                        {
                            if (nh > 0.3f) FloorPieces.WallBlock(gPar, c, -0.5f, nh + 0.02f, T, false);
                            FloorKit.ColliderBox(gCol, c + Vector3.up * (nh + 2f), new Vector3(T, 4f, T), 0f, "Colisor_Mata");
                            Vector3 p = c + new Vector3(Rf(-0.8f, 0.8f), nh, Rf(-0.8f, 0.8f));
                            if (front)
                            {
                                var d = FloorPieces.PickDeco(B1.scatter);
                                if (d != null) FloorPieces.Deco(gPar, NoCollider(d), p, Rf(0, 360));
                            }
                            else if (trees != null && trees.Length > 0)
                            {
                                var d = FloorPieces.PickDeco(trees);
                                var g = FloorKit.Sized(d.id, gPar, p, Rf(0, 360), Rf(d.min, Mathf.Max(d.min, d.max)), !string.IsNullOrEmpty(d.tint) ? Hex(d.tint, Color.white) : Color.white);
                                FloorKit.Occluder(g);
                            }
                            break;
                        }
                        default:
                        {
                            float top = front ? nh + 1.1f : nh + wallH;
                            FloorPieces.WallBlock(gPar, c, -0.5f, top, T, !front);
                            if (B1.wallStyle == "rocks" && !front && bigRocks.Length > 0 && Chance(0.3f) && !FloorKit.Full)
                            {
                                var d = FloorPieces.PickDeco(bigRocks);
                                var g = FloorKit.Sized(d.id, gPar, c + new Vector3(Rf(-0.6f, 0.6f), top - 0.7f, Rf(-0.6f, 0.6f)), Rf(0, 360), Rf(1.4f, 2.4f), Hex(string.IsNullOrEmpty(d.tint) ? B1.wallTint : d.tint, Color.white));
                                FloorKit.Occluder(g);
                            }
                            break;
                        }
                    }
                }
        }

        static DecoDef NoCollider(DecoDef d)
        {
            return new DecoDef { id = d.id, min = d.min, max = d.max, collider = false, occluder = false, lying = d.lying, tint = d.tint, weight = d.weight };
        }

        static DecoDef[] Filter(DecoDef[] l, bool big)
        {
            var r = new List<DecoDef>();
            if (l != null) foreach (var d in l) if (d != null && d.occluder == big) r.Add(d);
            return r.ToArray();
        }

        // ================================================================== entrada, chefe, santuário
        static void Entrance()
        {
            var e = path[0];
            Vector3 c = RoomCenter(e);
            FloorPieces.MakeSpawn(gSant, c, "entrance");
            Reserve(c, 2.5f);
            Vector3 portal = new Vector3(c.x, e.h, MinZ(e) + 2.2f);
            FloorPieces.MakePortal(gSant, portal);
            Reserve(portal, 2.5f);
            FloorPieces.biome = e.b;
            FloorPieces.StandingTorch(gLuz, portal + new Vector3(-2.8f, 0, 0.4f));
            FloorPieces.StandingTorch(gLuz, portal + new Vector3(2.8f, 0, 0.4f));
            lights += 2;
            Reserve(portal + new Vector3(-2.8f, 0, 0.4f), 0.8f);
            Reserve(portal + new Vector3(2.8f, 0, 0.4f), 0.8f);
            FloorPieces.biome = B1;
            // trilhas livres dentro das salas (do centro até cada corredor)
            foreach (var r in rooms)
                foreach (var cr in r.corrs)
                {
                    Vector2Int entry = EntryCell(cr, r);
                    Vector3 a = RoomCenter(r), b = C(entry);
                    b.y = r.h;
                    float len = Vector3.Distance(a, b);
                    for (float s = 0f; s <= len; s += 1.5f) lanes.Add(new Vector3(Mathf.Lerp(a.x, b.x, s / Mathf.Max(0.01f, len)), 1.6f, Mathf.Lerp(a.z, b.z, s / Mathf.Max(0.01f, len))));
                }
        }

        /// <summary>Célula da sala r por onde o corredor entra.</summary>
        static Vector2Int EntryCell(Corr c, Room r)
        {
            if (c.outside.Count == 0) return CenterCell(r);
            bool atA = c.a == r;
            int idx = atA ? c.outside[0] - 1 : c.outside[c.outside.Count - 1] + 1;
            idx = Mathf.Clamp(idx, 0, c.seq.Count - 1);
            return c.seq[idx];
        }

        static string ValidBoss(string id)
        {
            var d = GameData.Enemy(id);
            return d != null && d.boss ? id : null;
        }

        static string PickBoss()
        {
            if (!string.IsNullOrEmpty(S.boss) && S.boss != "-" && ValidBoss(S.boss) != null) return S.boss;
            var pool = new List<string>();
            if (B1.bosses != null) foreach (var b in B1.bosses) if (ValidBoss(b) != null) pool.Add(b);
            if (pool.Count == 0)
                foreach (var d in GameData.Enemies.Values) if (d.boss && !d.id.StartsWith("mb_")) pool.Add(d.id);
            return pool.Count > 0 ? Pick(pool) : null;
        }

        static void SanctuaryAndBoss()
        {
            Room bossRoom = path[path.Count - 1];
            Room sanct = rooms.Find(r => r.role == "santuario");
            if (sanct != null)
            {
                Vector3 c = RoomCenter(sanct);
                FloorPieces.biome = sanct.b;
                FloorPieces.MakeSanctuary(gSant, c);
                Reserve(c, 4.5f);
                lights++;
                FloorPieces.biome = B1;
            }
            if (bossRoom.role != "chefe")
            {
                if (sanct == null)
                {
                    Vector3 p = new Vector3(RoomCenter(bossRoom).x, bossRoom.h, MaxZ(bossRoom) - 2.5f);
                    FloorPieces.MakePortal(gSant, p);
                    Reserve(p, 2.5f);
                }
                return;
            }
            string id = PickBoss();
            if (id == null) { report.AppendLine("Aviso: nenhum chefe encontrado em enemies.json."); return; }
            Vector3 bc = RoomCenter(bossRoom);
            float backZ = MaxZ(bossRoom);
            Vector3 bp = new Vector3(bc.x, bossRoom.h, Mathf.Lerp(bc.z, backZ, 0.45f));
            // trono elevado (níveis > 0 e sala grande) — sem bloquear corredores
            if (S.levels > 0 && bossRoom.r.width >= 4 && bossRoom.r.height >= 4)
            {
                float w = Mathf.Min(bossRoom.r.width * T - 6f, 10f), d = 5f, h = LEVEL_H;
                float len = Mathf.CeilToInt(h / 0.3f) * 0.6f;
                float shift = Mathf.Max(0f, bossRoom.r.width * T * 0.5f - w * 0.5f - 1.5f);
                foreach (float dx in new[] { 0f, -shift, shift })
                {
                    Vector3 pc = new Vector3(bc.x + dx, bossRoom.h, backZ - d * 0.5f - 0.4f);
                    Vector3 fc = pc + new Vector3(0f, 0f, -len * 0.5f);   // plataforma + escada
                    if (!FreeRect(fc, w, d + len, true)) continue;
                    FloorPieces.biome = bossRoom.b;
                    FloorPieces.MakePlatform(gEsc, pc, 0f, w, d, h);
                    FloorPieces.biome = B1;
                    bp = pc + Vector3.up * h;
                    ReserveRect(fc, w, d + len + 1f);
                    break;
                }
            }
            var m = FloorPieces.MakeEnemy(gChefe, bp, id, EnemyRole.Chefe, null);
            Reserve(bp, 3f);
            enemyCount++;
            report.AppendLine("Chefe: " + (GameData.Enemy(id) != null ? GameData.Enemy(id).name : id));
            // braseiros com sombra
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = new Vector3(bc.x + s * (bossRoom.r.width * T * 0.5f - 2f), bossRoom.h, backZ - 2f);
                FloorPieces.biome = bossRoom.b;
                FloorPieces.Brazier(gLuz, p, shadowLights < 2);
                FloorPieces.biome = B1;
                shadowLights++;
                lights++;
                Reserve(p, 1.2f);
            }
            // escolta
            int esc = 2 + S.difficulty / 4;
            for (int k = 0; k < esc; k++) PlaceEnemy(bossRoom, 1f, false);
        }

        static void MiniBosses()
        {
            foreach (var r in path)
            {
                if (r.role != "minichefe") continue;
                var pool = new List<string>();
                var src = r.b.miniBosses != null && r.b.miniBosses.Length > 0 ? r.b.miniBosses : B1.miniBosses;
                if (src != null) foreach (var eid in src) if (GameData.Enemy(eid) != null) pool.Add(eid);
                if (pool.Count == 0) foreach (var d in GameData.Enemies.Values) if (d.id.StartsWith("mb_")) pool.Add(d.id);
                if (pool.Count == 0) { r.role = "normal"; continue; }
                string id = Pick(pool);
                Vector3 c = RoomCenter(r);
                FloorPieces.MakeEnemy(gMini, c, id, EnemyRole.MiniChefe, null);
                Reserve(c, 2.5f);
                enemyCount++;
                for (int k = 0; k < 2; k++) PlaceEnemy(r, 0.8f, false);
            }
        }

        // ================================================================== inimigos
        static readonly string[] EliteAbilities = { "investida", "escudo", "veneno", "teleporte", "furia", "fogo", "pedras" };

        static void Enemies()
        {
            int L = Mathf.Max(1, path.Count - 1);
            foreach (var r in rooms)
            {
                if (r.role == "entrada" || r.role == "santuario" || r.role == "secreta" || r.role == "chefe" || r.role == "minichefe") continue;
                float progress = r.path >= 0 ? r.path / (float)L : (r.parent != null ? r.parent.path / (float)L : 0.5f);
                float area = r.r.width * r.r.height;
                float n = area * 0.09f * Mathf.Max(0f, S.enemyDensity) * (0.8f + 0.7f * progress) * (1f + (S.difficulty - 1) * 0.05f);
                if (r.role == "lateral") n *= 0.6f;
                int count = Mathf.Clamp(Mathf.RoundToInt(n), S.enemyDensity > 0f ? 1 : 0, 10);
                for (int k = 0; k < count; k++) PlaceEnemy(r, progress, true);
            }
        }

        static void PlaceEnemy(Room r, float progress, bool allowElite)
        {
            var pool = new List<string>();
            if (r.b.enemies != null) foreach (var id in r.b.enemies) if (GameData.Enemy(id) != null && !GameData.Enemy(id).boss) pool.Add(id);
            if (pool.Count == 0) foreach (var d in GameData.Enemies.Values) if (!d.boss) pool.Add(d.id);
            if (pool.Count == 0) return;
            int maxIdx = Mathf.Clamp(2 + Mathf.RoundToInt(progress * (pool.Count - 2)) + S.difficulty / 4, 1, pool.Count);
            string eid = pool[Ri(0, maxIdx)];
            if (!FreePoint(r, 1.5f, 1.2f, false, out var p)) return;
            bool elite = allowElite && Chance(S.elitePercent / 100f);
            string[] ab = null;
            if (elite)
            {
                int na = S.difficulty >= 6 ? 2 : 1;
                var list = new List<string>();
                for (int k = 0; k < 6 && list.Count < na; k++)
                {
                    string a = EliteAbilities[Ri(0, EliteAbilities.Length)];
                    if (!list.Contains(a)) list.Add(a);
                }
                ab = list.ToArray();
                eliteCount++;
            }
            FloorPieces.MakeEnemy(gIni, p, eid, elite ? EnemyRole.Elite : EnemyRole.Normal, ab, elite ? 1.2f : 1f);
            Reserve(p, 1.2f);
            enemyCount++;
        }

        // ================================================================== portas
        static Vector3 V3(Vector2Int d) => new Vector3(d.x, 0f, d.y);

        /// <summary>Célula de corredor encostada na sala r (para porta) e a direção que aponta para dentro da sala.</summary>
        static bool DoorCell(Corr c, Room r, out Vector2Int cell, out Vector2Int dir)
        {
            cell = Vector2Int.zero; dir = Vector2Int.up;
            if (c.outside.Count == 0) return false;
            bool atA = c.a == r;
            int idx = atA ? c.outside[0] : c.outside[c.outside.Count - 1];
            int inner = atA ? idx - 1 : idx + 1;
            if (inner < 0 || inner >= c.seq.Count) return false;
            var ic = c.seq[inner];
            if (roomOf[ic.x, ic.y] != r.idx) return false;
            cell = c.seq[idx];
            if (used[cell.x, cell.y]) return false;
            dir = ic - cell;
            return true;
        }

        /// <summary>Porta na face entre a célula do corredor e a sala (parede secreta: ocupa a célula inteira).</summary>
        static Drakantus.Door PlaceDoor(Corr c, Room r, Vector2Int cell, Vector2Int dir, string style, bool startOpen)
        {
            used[cell.x, cell.y] = true;
            Vector3 pos = C(cell);
            if (style != "secreta") pos += V3(dir) * (T * 0.5f - 0.4f);
            pos.y = r.h;
            float width = T;
            Vector2Int p = Perp(dir);
            if (!c.narrow)
            {
                width = T * 2f;
                pos += V3(p) * (T * 0.5f);
                var sib = cell + p;
                if (In(sib.x, sib.y)) used[sib.x, sib.y] = true;
            }
            float height = Mathf.Max(2.6f, B1.wallHeight);
            var d = FloorPieces.MakeDoor(gPortas, pos, V3(dir), V3(p), width, height, style, startOpen);
            Reserve(pos, 1.6f);
            return d;
        }

        static void Doors()
        {
            if (!S.arenaDoors) return;
            foreach (var r in rooms)
            {
                if (r.role != "chefe" && r.role != "minichefe") continue;
                var doors = new List<FloorReceiver>();
                foreach (var c in r.corrs)
                {
                    if (c.kind == "secret") continue;
                    if (!DoorCell(c, r, out var cell, out var dir)) continue;
                    bool toSanct = c.kind == "sanct";
                    doors.Add(PlaceDoor(c, r, cell, dir, "grade", !toSanct));
                }
                if (doors.Count == 0) continue;
                Vector3 rc = RoomCenter(r);
                var trig = FloorPieces.MakeRoomClear(gGat, rc, new Vector3(r.r.width * T - 1.5f, 6f, r.r.height * T - 1.5f));
                if (r.role == "minichefe")
                {
                    trig.lockMessage = "Mini-chefe! As portas se fecharam.";
                    trig.clearMessage = "Mini-chefe derrotado! Um baú apareceu.";
                    Vector3 cp = new Vector3(rc.x, r.h, MaxZ(r) - 2f);
                    doors.Add(FloorPieces.MakeChest(gBau, cp, 2, true));
                    Reserve(cp, 1.2f);
                    chestCount++;
                    rareFromArenas++;
                }
                else
                {
                    trig.lockMessage = "A arena se fechou! Derrote o chefe.";
                    trig.clearMessage = "Arena liberada!";
                }
                FloorPieces.Link(trig.gameObject, doors.ToArray());
            }
        }

        static int rareFromArenas;

        static void LeverDoor()
        {
            if (!S.lockedDoor || path.Count < 3) return;
            var order = new List<int>();
            for (int k = 1; k < path.Count - 1; k++) order.Add(k);
            order.Sort((x, y) => Mathf.Abs(x - path.Count / 2).CompareTo(Mathf.Abs(y - path.Count / 2)));
            foreach (int k in order)
            {
                Room from = path[k], to = path[k + 1];
                if (to.role == "chefe" || to.role == "minichefe") continue;
                Corr c = corrs.Find(x => x.kind == "path" && x.a == from && x.b == to);
                if (c == null || !DoorCell(c, to, out var cell, out var dir)) continue;
                // sala da alavanca: lateral ligada antes da porta, senão a própria sala k
                Room leverRoom = rooms.Find(x => x.role == "lateral" && x.parent != null && x.parent.path <= k);
                if (leverRoom == null) leverRoom = from;
                if (!LeverSpot(leverRoom, out var lp, out var ldir)) continue;
                var door = PlaceDoor(c, to, cell, dir, "pedra", false);
                FloorPieces.biome = leverRoom.b;
                var lever = FloorPieces.MakeLever(gPortas, lp, ldir);
                FloorPieces.biome = B1;
                Reserve(lp, 1.5f);
                FloorPieces.Link(lever.gameObject, door);
                report.AppendLine("Porta trancada entre as salas " + k + " e " + (k + 1) + "; alavanca na sala " + (leverRoom.role == "lateral" ? "lateral" : k.ToString()) + ".");
                return;
            }
            report.AppendLine("Aviso: não achei lugar para a porta trancada com alavanca.");
        }

        /// <summary>Ponto encostado na parede do fundo (norte) ou leste da sala, sem corredor na frente.</summary>
        static bool LeverSpot(Room r, out Vector3 pos, out Vector3 dir)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                bool north = tries % 2 == 0;
                if (north)
                {
                    int x = Ri(r.r.xMin, r.r.xMax), j = r.r.yMax - 1;
                    if (IsFloor(x, j + 1)) continue;
                    pos = C(x, j) + new Vector3(Rf(-1f, 1f), r.h, T * 0.5f - 0.6f);
                    dir = Vector3.back;
                }
                else
                {
                    int z = Ri(r.r.yMin, r.r.yMax), i = r.r.xMax - 1;
                    if (IsFloor(i + 1, z)) continue;
                    pos = C(i, z) + new Vector3(T * 0.5f - 0.6f, r.h, Rf(-1f, 1f));
                    dir = Vector3.left;
                }
                if (Free(pos, 1f, false)) return true;
            }
            pos = RoomCenter(r); dir = Vector3.back;
            return false;
        }

        static void Secret()
        {
            var r = rooms.Find(x => x.role == "secreta");
            if (r == null) return;
            var c = corrs.Find(x => x.kind == "secret" && x.b == r);
            if (c == null || !DoorCell(c, r.parent, out var cell, out var dir))
            {
                report.AppendLine("Aviso: sala secreta ficou sem parede falsa (corredor curto).");
                return;
            }
            var door = PlaceDoor(c, r.parent, cell, dir, "secreta", false);
            var lv = door.gameObject.AddComponent<Drakantus.Lever>();
            lv.hidden = true;
            lv.once = true;
            lv.radius = 2.8f;
            lv.label = "[E] Examinar a parede rachada";
            FloorPieces.Link(door.gameObject, door);
            Vector3 rc = RoomCenter(r);
            FloorPieces.MakeChest(gBau, rc + new Vector3(0, 0, 1f), 2, false);
            Reserve(rc, 2f);
            chestCount++;
            secretChest = true;
            FloorKit.Sized("Gold_Nuggets", gDeco, rc + new Vector3(-1.8f, 0, 1.5f), Rf(0, 360), 0.5f, Color.white);
            FloorKit.Sized("Gold_Bars_Stack_Small", gDeco, rc + new Vector3(1.8f, 0, 1.2f), Rf(0, 360), 0.6f, Color.white);
            report.AppendLine("Sala secreta: parede rachada (tecla E) perto da sala " + (r.parent.path >= 0 ? r.parent.path.ToString() : "?") + ".");
        }

        static bool secretChest;

        // ================================================================== armadilhas e névoa
        static void Traps()
        {
            var cells = new List<Vector2Int>();
            foreach (var c in corrs)
            {
                if (c.kind == "secret" || c.kind == "sanct") continue;
                foreach (int idx in c.outside)
                {
                    var cell = c.seq[idx];
                    if (used[cell.x, cell.y] || !Flat(cell) || cells.Contains(cell)) continue;
                    cells.Add(cell);
                }
            }
            for (int k = cells.Count - 1; k > 0; k--) { int r = Ri(0, k + 1); var tmp = cells[k]; cells[k] = cells[r]; cells[r] = tmp; }
            int ci = 0;
            float mult = 1f;
            for (int k = 0; k < S.hiddenTraps; k++)
            {
                if (ci < cells.Count)
                {
                    var cell = cells[ci++];
                    used[cell.x, cell.y] = true;
                    FloorPieces.MakeSpikes(gArm, C(cell) + Vector3.up * hIn[cell.x, cell.y], true, 3f, 12f * mult);
                    trapCount++;
                }
                else if (TrapInRoom(out var p)) { FloorPieces.MakeSpikes(gArm, p, true, 2.5f, 12f); Reserve(p, 1.5f); trapCount++; }
            }
            for (int k = 0; k < S.visibleTraps; k++)
            {
                int v = k % 3;
                if (ci < cells.Count)
                {
                    var cell = cells[ci++];
                    used[cell.x, cell.y] = true;
                    Vector3 pos = C(cell) + Vector3.up * hIn[cell.x, cell.y];
                    if (v == 2 && ArrowCorridor(cell, pos)) { trapCount++; continue; }
                    if (v == 0) FloorPieces.MakeSpikes(gArm, pos, false, 3f, 12f);
                    else FloorPieces.MakeFireTrap(gArm, pos, 10f, false);
                    trapCount++;
                }
                else if (TrapInRoom(out var p))
                {
                    if (v == 0) FloorPieces.MakeSpikes(gArm, p, false, 2.5f, 12f);
                    else FloorPieces.MakeFireTrap(gArm, p, 10f, false);
                    Reserve(p, 1.5f);
                    trapCount++;
                }
            }
        }

        /// <summary>Placa no corredor estreito + lançadores de flecha nas duas paredes.</summary>
        static bool ArrowCorridor(Vector2Int cell, Vector3 pos)
        {
            Vector2Int d = cdir[cell.x, cell.y];
            if (d == Vector2Int.zero) return false;
            Vector2Int p = Perp(d);
            if (IsFloor(cell.x + p.x, cell.y + p.y) || IsFloor(cell.x - p.x, cell.y - p.y)) return false;
            var plate = FloorPieces.MakePlate(gArm, pos, false);
            Vector3 pv = V3(p);
            var a1 = FloorPieces.MakeArrowTrap(gArm, pos + pv * (T * 0.5f - 0.05f) + Vector3.up * 1.1f, -pv, 8f, true);
            var a2 = FloorPieces.MakeArrowTrap(gArm, pos - pv * (T * 0.5f - 0.05f) + Vector3.up * 1.1f, pv, 8f, true);
            FloorPieces.Link(plate, a1.GetComponent<Drakantus.ArrowTrap>(), a2.GetComponent<Drakantus.ArrowTrap>());
            return true;
        }

        static bool TrapInRoom(out Vector3 p)
        {
            p = Vector3.zero;
            var cands = rooms.FindAll(r => r.role == "normal" || r.role == "lateral");
            if (cands.Count == 0) return false;
            for (int k = 0; k < 6; k++)
                if (FreePoint(Pick(cands), 2f, 1.6f, false, out p)) return true;
            return false;
        }

        static void Fog()
        {
            if (!S.poisonFog) return;
            var cands = rooms.FindAll(r => (r.role == "normal" || r.role == "lateral") && r.path != 0);
            int n = Mathf.Min(cands.Count, Mathf.Max(1, rooms.Count / 4));
            for (int k = 0; k < n; k++)
            {
                var r = cands[Ri(0, cands.Count)];
                cands.Remove(r);
                float rad = Rf(2.5f, 3.5f);
                if (!FreePoint(r, rad, 1f, false, out var p)) continue;
                FloorPieces.MakeFog(gArm, p, rad, 5f + S.difficulty * 0.6f);
                Reserve(p, rad * 0.6f);
                trapCount++;
            }
        }

        // ================================================================== baús e plataformas
        static void Chests()
        {
            int rare = Mathf.Max(0, S.rareChests - rareFromArenas - (secretChest ? 1 : 0));
            int common = Mathf.Max(0, S.commonChests);
            var side = rooms.FindAll(r => r.role == "lateral");
            var normal = rooms.FindAll(r => r.role == "normal");
            for (int k = 0; k < rare + common; k++)
            {
                int tier = k < rare ? 2 : 1;
                Room r = side.Count > 0 && (k < side.Count || Chance(0.4f)) ? side[k % side.Count] : (normal.Count > 0 ? Pick(normal) : null);
                if (r == null) break;
                Vector3 p = new Vector3(Rf(MinX(r) + 2f, MaxX(r) - 2f), r.h, MaxZ(r) - 1.6f);
                if (!Free(p, 1.2f, true) && !FreePoint(r, 1.8f, 1.2f, true, out p)) continue;
                FloorPieces.MakeChest(gBau, p, tier, false);
                Reserve(p, 1.3f);
                chestCount++;
            }
            secretChest = false;
            rareFromArenas = 0;
        }

        // [Quebraveis] barris, caixas, vasos e barris explosivos nas salas comuns/laterais.
        // Usa um gerador próprio (derivado da semente) para não mudar o resto do andar gerado com a mesma semente.
        static void Breakables()
        {
            var brng = new Random(S.seed * 31 + 977);
            float F(float a, float b) => a + (float)brng.NextDouble() * (b - a);
            var cands = rooms.FindAll(r => r.role == "normal" || r.role == "lateral" || r.role == "minichefe");
            int placed = 0;
            foreach (var r in cands)
            {
                int want = 2 + brng.Next(0, 3);
                for (int k = 0, tries = 0; k < want && tries < 25; tries++)
                {
                    float x0 = MinX(r) + 1.6f, x1 = MaxX(r) - 1.6f, z0 = MinZ(r) + 1.6f, z1 = MaxZ(r) - 1.6f;
                    if (x1 <= x0 || z1 <= z0) break;
                    var p = new Vector3(F(x0, x1), r.h, F(z0, z1));
                    if (!Free(p, 1.1f, true)) continue;
                    double roll = brng.NextDouble();
                    string kind = roll < 0.18 ? "explosive" : roll < 0.45 ? "barrel" : roll < 0.68 ? "crate" : roll < 0.85 ? "crate_small" : "vase";
                    FloorPieces.MakeBreakable(gDeco, p, kind, F(0f, 360f));
                    Reserve(p, 0.9f);
                    k++; placed++;
                }
            }
            if (placed > 0) report.AppendLine("Quebráveis: " + placed);
        }

        static void Platforms()
        {
            if (S.levels <= 0) return;
            foreach (var r in rooms)
            {
                if (r.role != "normal" || r.r.width < 5 || r.r.height < 5 || !Chance(0.4f)) continue;
                float w = Mathf.Min(r.r.width * T - 6f, 8f), d = 4f, h = LEVEL_H;
                float len = Mathf.CeilToInt(h / 0.3f) * 0.6f;
                Vector3 c = RoomCenter(r);
                Vector3 pc = new Vector3(c.x, r.h, MaxZ(r) - d * 0.5f - 0.3f);
                Vector3 fc = pc + new Vector3(0f, 0f, -len * 0.5f);
                if (!FreeRect(fc, w, d + len, true)) continue;
                FloorPieces.biome = r.b;
                FloorPieces.MakePlatform(gEsc, pc, 0f, w, d, h);
                FloorPieces.biome = B1;
                ReserveRect(fc, w, d + len + 1f);
                if (GameData.Enemy("archer") != null)
                {
                    FloorPieces.MakeEnemy(gIni, pc + Vector3.up * h, "archer", EnemyRole.Normal, null);
                    enemyCount++;
                }
            }
        }

        // ================================================================== luzes
        static void Lights()
        {
            const int maxLights = 34;
            foreach (var r in rooms)
            {
                if (r.role == "santuario") continue;
                var b = r.b;
                float dens = Mathf.Clamp01(b.torchDensity);
                int n = Mathf.RoundToInt((r.r.width + r.r.height) * T * 2f / 16f * dens);
                if (b.dark) n = Mathf.Max(n, 1);
                n = Mathf.Clamp(n, 0, 4);
                var placed = new List<Vector3>();
                FloorPieces.biome = b;
                for (int tries = 0; tries < n * 4 && placed.Count < n && lights < maxLights; tries++)
                {
                    bool north = Chance(0.6f);
                    Vector3 face, dir;
                    if (north)
                    {
                        int x = Ri(r.r.xMin, r.r.xMax), j = r.r.yMax - 1;
                        if (IsFloor(x, j + 1)) continue;
                        face = C(x, j) + new Vector3(0, 0, T * 0.5f);
                        dir = Vector3.back;
                    }
                    else
                    {
                        int z = Ri(r.r.yMin, r.r.yMax), i = r.r.xMax - 1;
                        if (IsFloor(i + 1, z)) continue;
                        face = C(i, z) + new Vector3(T * 0.5f, 0, 0);
                        dir = Vector3.left;
                    }
                    bool near = false;
                    foreach (var q in placed) if ((q - face).sqrMagnitude < 8f * 8f) { near = true; break; }
                    if (near) continue;
                    if (B1.wallStyle == "trees")
                    {
                        // ao ar livre: tocha de pé perto da borda
                        Vector3 p = face + dir * 1.4f;
                        p.y = r.h;
                        if (!Free(p, 0.8f, true)) continue;
                        FloorPieces.StandingTorch(gLuz, p);
                        Reserve(p, 0.8f);
                    }
                    else FloorPieces.WallTorch(gLuz, face, dir, r.h);
                    placed.Add(face);
                    lights++;
                }
            }
            // corredores longos em biomas escuros
            if (B1.dark)
                foreach (var c in corrs)
                {
                    if (lights >= maxLights) break;
                    if (c.outside.Count < 4 || c.kind == "secret") continue;
                    var cell = c.seq[c.outside[c.outside.Count / 2]];
                    Vector2Int d = cdir[cell.x, cell.y];
                    if (d == Vector2Int.zero) continue;
                    Vector2Int p = Perp(d);
                    if (IsFloor(cell.x + p.x, cell.y + p.y)) continue;
                    FloorPieces.biome = B1;
                    FloorPieces.WallTorch(gLuz, C(cell) + V3(p) * (T * 0.5f), -V3(p), hIn[cell.x, cell.y]);
                    lights++;
                }
            FloorPieces.biome = B1;
        }

        // ================================================================== decoração
        static void Decorate()
        {
            foreach (var r in rooms)
            {
                if (FloorKit.Full) break;
                var b = r.b;
                FloorPieces.biome = b;
                float area = r.r.width * r.r.height * T * T;
                // lava
                if (b.hazard == "lava" && (r.role == "normal" || r.role == "lateral") && r.r.width >= 5 && r.r.height >= 5)
                {
                    int pools = Ri(1, 3);
                    for (int k = 0; k < pools; k++)
                    {
                        int ci = Ri(r.r.xMin + 2, r.r.xMax - 2), cj = Ri(r.r.yMin + 2, r.r.yMax - 2);
                        Vector3 p = C(ci, cj) + Vector3.up * r.h;
                        if (!Free(p, 2.6f, true)) continue;
                        FloorPieces.Primitive(gDeco, "lava", p, 0f, Rf(1f, 1.4f), Color.white);
                        lavaCell[ci, cj] = true;
                        Reserve(p, 2.6f);
                    }
                }
                Category(r, b.ruins, b.ruinDensity, area, 1.8f, true);
                Category(r, b.trees, b.treeDensity, area, 1.2f, true);
                Category(r, b.rocks, b.rockDensity, area, 0.8f, true);
                Category(r, b.special, b.specialDensity, area, 1.0f, true);
                Category(r, b.props, b.propDensity * (r.role == "lateral" ? 1.5f : 1f), area, 0.8f, true);
                Category(r, b.scatter, b.scatterDensity, area, 0.35f, false);
            }
            FloorPieces.biome = B1;
        }

        static void Category(Room r, DecoDef[] list, float density, float area, float rad, bool reserve)
        {
            if (list == null || list.Length == 0 || density <= 0f) return;
            int n = Mathf.RoundToInt(area / 100f * density * Rf(0.7f, 1.3f));
            if (r.role == "chefe" || r.role == "minichefe") n = n / 2;
            for (int k = 0; k < n && !FloorKit.Full; k++)
            {
                var d = FloorPieces.PickDeco(list);
                if (d == null) continue;
                float rr = d.occluder ? Mathf.Max(rad, 1.6f) : rad;
                bool block = d.collider || d.occluder;
                if (!FreePoint(r, block ? 1.6f : 0.6f, rr, block, out var p)) continue;
                FloorPieces.Deco(gDeco, d, p, Rf(0, 360));
                if (reserve) Reserve(p, rr);
            }
        }

        static void Backdrop()
        {
            var list = B1.backdrop;
            if (list == null || list.Length == 0) return;
            int placed = 0;
            for (int i = 0; i < GW && placed < 160; i++)
                for (int j = 0; j < GH && placed < 160; j++)
                {
                    if (kind[i, j] != 0 || FloorKit.Full) continue;
                    int dist = 99;
                    for (int di = -3; di <= 3; di++)
                        for (int dj = -3; dj <= 3; dj++)
                            if (IsFloor(i + di, j + dj)) dist = Mathf.Min(dist, Mathf.Max(Mathf.Abs(di), Mathf.Abs(dj)));
                    if (dist < 2 || dist > 3 || !Chance(0.45f)) continue;
                    // não tampa a vista: atrás (norte/leste) mais denso que na frente da câmera
                    bool frontSide = IsFloor(i + 2, j) || IsFloor(i, j + 2) || IsFloor(i + 2, j + 2);
                    if (frontSide && !Chance(0.25f)) continue;
                    var d = FloorPieces.PickDeco(list);
                    if (d == null) continue;
                    var c = C(i, j) + new Vector3(Rf(-1.2f, 1.2f), 0f, Rf(-1.2f, 1.2f));
                    var g = FloorPieces.Deco(gDeco, NoCollider(d), c, Rf(0, 360));
                    if (g != null && d.occluder) FloorKit.Occluder(g);
                    placed++;
                }
        }

        static void Ambient()
        {
            foreach (var r in rooms)
            {
                var b = r.b;
                bool ok;
                var k = FloorKit.FxKindFromName(b.particles, out ok);
                if (!ok) continue;
                Color c = Hex(b.particleColor, Color.white);
                switch (k)
                {
                    case FloorFxKind.Areia: c.a = 0.35f; break;
                    case FloorFxKind.Nevoa: c.a = 0.16f; break;
                    case FloorFxKind.Cinzas: c.a = 0.6f; break;
                    case FloorFxKind.Fumaca: c.a = 0.25f; break;
                    case FloorFxKind.Neve: c.a = 0.9f; break;
                    default: c.a = 1f; break;
                }
                float w = r.r.width * T, d = r.r.height * T;
                float hgt = k == FloorFxKind.Neve ? 9f : k == FloorFxKind.Folhas ? 7f : 3.5f;
                int amount = Mathf.Clamp(Mathf.RoundToInt(w * d / (k == FloorFxKind.Nevoa ? 30f : 8f)), 12, k == FloorFxKind.Neve ? 220 : 120);
                Vector3 pos = RoomCenter(r) + Vector3.up * (k == FloorFxKind.Nevoa ? 0.4f : k == FloorFxKind.Neve || k == FloorFxKind.Folhas ? 0f : hgt * 0.5f);
                FloorKit.Fx(gFx, pos, k, new Vector3(w, hgt, d), c, amount);
            }
        }
    }
}
