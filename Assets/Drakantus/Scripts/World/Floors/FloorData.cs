using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    // =====================================================================================
    //  Dados dos andares criados no editor (Criador de Andares)
    //  - Resources/Data/biomes.json  → BiomeDef (visual, luz, decoração, inimigos de cada bioma)
    //  - Resources/Data/bosses.json  → BossProfile (habilidades de elites, mini-chefes e chefes)
    //  - Resources/Data/floors.json  → FloorEntry (lista de andares salvos; lida por FloorRegistry)
    //  - Resources/Floors/<id>.prefab → o andar em si (FloorRoot + cenário + marcadores)
    // =====================================================================================

    /// <summary>Um modelo de decoração do bioma (id do ModelLibrary ou "#primitiva").</summary>
    [Serializable]
    public class DecoDef
    {
        public string id = "";
        public float min = 1f, max = 1f;     // altura-alvo em metros (sorteada entre min e max)
        public bool collider, occluder, lying;
        public string tint = "";             // "" = usa decoTint do bioma
        public float weight = 1f;
    }

    /// <summary>Bioma: materiais/cores, decoração por categoria, iluminação, partículas, música e inimigos.</summary>
    [Serializable]
    public class BiomeDef
    {
        public string id = "", name = "", category = "", desc = "";
        // chão
        public string floorBlock = "stone", floorBlockAlt = "stone_dark", floorTint = "ffffff", floorAltTint = "";
        public float floorAltChance = 0.2f;
        public string baseColor = "202024";          // laje de fundo (fora das salas)
        // paredes: "blocks" (blocos empilhados), "trees" (mata fechada), "rocks" (rochedos)
        public string wallStyle = "blocks";
        public string wallBlock = "bricks_A", wallBlockAlt = "bricks_B", wallTint = "ffffff";
        public float wallHeight = 3.5f;
        // decoração (cada categoria tem densidade por 100 m² de sala)
        public string decoTint = "";
        public DecoDef[] trees, rocks, props, ruins, scatter, special, backdrop;
        public float treeDensity, rockDensity = 0.4f, propDensity = 0.5f, ruinDensity, scatterDensity = 1f, specialDensity;
        // luz
        public string sun = "fff0d8", ambient = "404650", fog = "303540";
        public float sunIntensity = 1f, fogDensity = 0.02f, sunPitch = 50f, sunYaw = -30f;
        public bool dark;
        public string torchColor = "ff9e4a";
        public float torchDensity = 0.5f;            // 0 = sem tochas, 1 = muitas
        // ambiente
        public string particles = "";                // neve, areia, brasas, vagalumes, poeira, cinzas, fumaca, nevoa, folhas
        public string particleColor = "ffffff";
        public string music = "dungeon";
        public string hazard = "";                   // "lava" = poças de lava nas salas
        // inimigos
        public string enemyTint = "ffffff";
        public string[] enemies;                     // ids de enemies.json (os primeiros aparecem mais no começo)
        public string[] miniBosses;
        public string[] bosses;
    }

    [Serializable] public class BiomeList { public BiomeDef[] biomes; }

    /// <summary>Habilidades extras de um inimigo (por id de enemies.json).</summary>
    [Serializable]
    public class BossProfile
    {
        public string id = "";
        public string[] abilities;        // desde o início
        public string[] phaseAbilities;   // ganhas a cada 33% de vida perdida (chefes)
        public bool phases;
        public string aura = "";          // cor da aura (hex) — "" = sem aura
    }

    [Serializable] public class BossList { public BossProfile[] profiles; }

    /// <summary>Catálogo de biomas (Resources/Data/biomes.json).</summary>
    public static class Biomes
    {
        static List<BiomeDef> list;
        static Dictionary<string, BiomeDef> map;

        public static void Reload() { list = null; map = null; }

        static void Load()
        {
            if (list != null) return;
            list = new List<BiomeDef>();
            map = new Dictionary<string, BiomeDef>();
            var ta = Resources.Load<TextAsset>("Data/biomes");
            if (ta == null) { Debug.LogWarning("[Drakantus] Faltando Resources/Data/biomes.json"); return; }
            try
            {
                var l = JsonUtility.FromJson<BiomeList>(ta.text);
                if (l != null && l.biomes != null)
                    foreach (var b in l.biomes)
                        if (b != null && !string.IsNullOrEmpty(b.id)) { list.Add(b); map[b.id] = b; }
            }
            catch (Exception ex) { Debug.LogError("[Drakantus] Erro lendo biomes.json: " + ex.Message); }
        }

        public static List<BiomeDef> All() { Load(); return list; }

        public static BiomeDef Get(string id)
        {
            Load();
            if (id != null && map.TryGetValue(id, out var b)) return b;
            return null;
        }
    }

    /// <summary>Perfis de habilidades (Resources/Data/bosses.json).</summary>
    public static class BossProfiles
    {
        static Dictionary<string, BossProfile> map;

        public static void Reload() { map = null; }

        public static BossProfile Get(string enemyId)
        {
            if (map == null)
            {
                map = new Dictionary<string, BossProfile>();
                var ta = Resources.Load<TextAsset>("Data/bosses");
                if (ta != null)
                {
                    try
                    {
                        var l = JsonUtility.FromJson<BossList>(ta.text);
                        if (l != null && l.profiles != null)
                            foreach (var p in l.profiles)
                                if (p != null && !string.IsNullOrEmpty(p.id)) map[p.id] = p;
                    }
                    catch (Exception ex) { Debug.LogError("[Drakantus] Erro lendo bosses.json: " + ex.Message); }
                }
            }
            if (enemyId != null && map.TryGetValue(enemyId, out var r)) return r;
            return null;
        }
    }

    // =====================================================================================
    //  FloorRegistry — lista de andares para a UI (janela da Torre) e para o Game
    // =====================================================================================

    [Serializable]
    public class FloorEntry
    {
        public string id = "";
        public string name = "";
        public string subtitle = "";
        public string biome = "";
        public string next = "";
        public int floor;
        public int recommendedLevel = 1;
        public int difficulty = 1;
        public bool builtIn;              // true = andar feito à mão no LevelBuilder (tower_f1, tower_f2)
    }

    [Serializable]
    public class FloorList
    {
        public List<FloorEntry> floors = new List<FloorEntry>();
    }

    /// <summary>
    /// Andares da Torre. Uso pela UI: <c>foreach (var f in FloorRegistry.List()) ... Game.I.EnterTower(f.id)</c>.
    /// Junta os andares fixos (tower_f1, tower_f2) com os salvos pelo Criador de Andares em
    /// Resources/Data/floors.json (prefabs em Resources/Floors/&lt;id&gt;.prefab). Ordenado pelo número do andar.
    /// </summary>
    public static class FloorRegistry
    {
        public const string JsonResource = "Data/floors";
        public const string PrefabFolder = "Floors/";
        static List<FloorEntry> cache;

        static List<FloorEntry> BuiltIn()
        {
            return new List<FloorEntry>
            {
                new FloorEntry { id = "tower_f1", name = "Bosque Esmeralda", subtitle = "Andar 01 · Bosque Esmeralda", biome = "floresta", floor = 1, recommendedLevel = 1, difficulty = 1, next = "tower_f2", builtIn = true },
                new FloorEntry { id = "tower_f2", name = "Ruínas Sombrias", subtitle = "Andar 02 · Ruínas Sombrias", biome = "ruinas", floor = 2, recommendedLevel = 4, difficulty = 3, next = "", builtIn = true },
            };
        }

        /// <summary>Lista de andares (cópia), ordenada pelo número do andar.</summary>
        public static List<FloorEntry> List()
        {
            if (cache == null) Reload();
            return new List<FloorEntry>(cache);
        }

        /// <summary>Relê floors.json (o editor chama depois de salvar um andar).</summary>
        public static void Reload()
        {
            var map = new Dictionary<string, FloorEntry>();
            var order = new List<string>();
            foreach (var f in BuiltIn()) { map[f.id] = f; order.Add(f.id); }
            var fl = ReadJson();
            foreach (var f in fl.floors)
            {
                if (f == null || string.IsNullOrEmpty(f.id)) continue;
                if (!map.ContainsKey(f.id)) order.Add(f.id);
                f.builtIn = false;
                map[f.id] = f;
            }
            cache = new List<FloorEntry>();
            foreach (var id in order) cache.Add(map[id]);
            cache.Sort((a, b) => a.floor != b.floor ? a.floor.CompareTo(b.floor) : string.CompareOrdinal(a.id, b.id));
        }

        public static FloorEntry Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache == null) Reload();
            foreach (var f in cache) if (f.id == id) return f;
            return null;
        }

        /// <summary>Existe prefab salvo em Resources/Floors/&lt;id&gt;?</summary>
        public static bool HasPrefab(string id) => !string.IsNullOrEmpty(id) && Resources.Load<GameObject>(PrefabFolder + id) != null;

        /// <summary>O Game consegue viajar para este id? (andar fixo ou prefab salvo)</summary>
        public static bool CanLoad(string id) => id == "tower_f1" || id == "tower_f2" || HasPrefab(id);

        /// <summary>Id do andar de número floor+1 que pode ser carregado ("" se não houver).</summary>
        public static string NextAfter(int floor)
        {
            if (cache == null) Reload();
            foreach (var f in cache) if (f.floor == floor + 1 && CanLoad(f.id)) return f.id;
            return "";
        }

        public static FloorList ReadJson()
        {
            var ta = Resources.Load<TextAsset>(JsonResource);
            if (ta == null || string.IsNullOrWhiteSpace(ta.text)) return new FloorList();
            try
            {
                var l = JsonUtility.FromJson<FloorList>(ta.text);
                if (l == null) return new FloorList();
                if (l.floors == null) l.floors = new List<FloorEntry>();
                return l;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Drakantus] Erro lendo floors.json: " + ex.Message);
                return new FloorList();
            }
        }
    }
}
