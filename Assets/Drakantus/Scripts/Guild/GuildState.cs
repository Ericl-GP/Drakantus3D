using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Drakantus
{
    // ======================================================================
    // Dados da Guilda (Resources/Data/quests.json, ranks.json, pets.json)
    // ======================================================================

    /// <summary>Missão do quadro. type: "daily" | "hunt" | "story" | "promo".
    /// goal: "kill" (target = id do inimigo ou "any"), "boss" (target = id do chefe ou "any"),
    /// "collect" (target = "coin" | "item" | "any"), "skill" (usos de habilidade), "floor" (target = número do andar ou "any").
    /// type "chain" = Missão de Sequência rara (chain = id da cadeia, chainStep = 1..N). targetTime = meta de tempo (s) para a nota; 0 = padrão.</summary>
    [System.Serializable]
    public class QuestDef
    {
        public string id, type, title, desc, goal, target;
        public int count = 1, coins, xp, points, minRank, order;
        public string[] items;
        public string chain;
        public int chainStep;
        public float targetTime;
    }

    [System.Serializable] class QuestList { public QuestDef[] quests; }

    [System.Serializable]
    public class RankTrial
    {
        public string title, desc, goal, target;
        public int count = 1, coins, xp;
    }

    [System.Serializable]
    public class RankDef
    {
        public string id, name, title, color, desc;
        public int points, coins;
        public string[] items;
        public RankTrial trial;
    }

    [System.Serializable] class RankList { public RankDef[] ranks; }

    /// <summary>Pet à venda na loja da Lumi. shape: "slime" | "dragaozinho" | "coruja" | "gatinho" | "cogumelo" | "fantasminha".</summary>
    [System.Serializable]
    public class PetDef
    {
        public string id, name, species, shape, color, color2, eyes, desc, special, specialDesc;
        public int price;
        public float speed = 6f, radius = 6f, dmg = 0.6f, size = 1f;
    }

    [System.Serializable] class PetList { public PetDef[] pets; }

    public static class GuildData
    {
        public static readonly Dictionary<string, QuestDef> Quests = new();
        public static readonly List<QuestDef> QuestOrder = new();
        public static readonly List<RankDef> Ranks = new();
        public static readonly Dictionary<string, PetDef> Pets = new();
        public static readonly List<PetDef> PetOrder = new();
        static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            var q = Read<QuestList>("quests");
            if (q != null && q.quests != null)
                foreach (var x in q.quests)
                {
                    if (x == null || string.IsNullOrEmpty(x.id)) continue;
                    if (x.count <= 0) x.count = 1;
                    if (string.IsNullOrEmpty(x.target)) x.target = "any";
                    Quests[x.id] = x; QuestOrder.Add(x);
                }
            var r = Read<RankList>("ranks");
            if (r != null && r.ranks != null) foreach (var x in r.ranks) if (x != null) Ranks.Add(x);
            if (Ranks.Count == 0) Ranks.Add(new RankDef { id = "F", name = "F", title = "Novato", color = "9a9a9a" });
            var p = Read<PetList>("pets");
            if (p != null && p.pets != null)
                foreach (var x in p.pets)
                {
                    if (x == null || string.IsNullOrEmpty(x.id)) continue;
                    if (x.speed <= 0f) x.speed = 6f;
                    if (x.radius <= 0f) x.radius = 6f;
                    if (x.size <= 0f) x.size = 1f;
                    Pets[x.id] = x; PetOrder.Add(x);
                }
        }

        static T Read<T>(string name) where T : class
        {
            var ta = Resources.Load<TextAsset>("Data/" + name);
            if (ta == null) { Debug.LogWarning("[Drakantus] Faltando Resources/Data/" + name + ".json"); return null; }
            try { return JsonUtility.FromJson<T>(ta.text); }
            catch (System.Exception ex) { Debug.LogError("[Drakantus] Erro lendo " + name + ".json: " + ex.Message); return null; }
        }

        public static QuestDef Quest(string id) { Load(); return id != null && Quests.TryGetValue(id, out var q) ? q : null; }
        public static PetDef Pet(string id) { Load(); return id != null && Pets.TryGetValue(id, out var p) ? p : null; }
        public static RankDef Rank(int i) { Load(); return Ranks[Mathf.Clamp(i, 0, Ranks.Count - 1)]; }
    }

    // ======================================================================
    // Estado salvo (separado do save principal): drakantus_guild.json
    // ======================================================================

    /// <summary>Progresso de uma missão aceita. v = 1: estatísticas da nota rastreadas (saves antigos têm v = 0).
    /// time = segundos de jogo desde aceitar (para ao concluir), dmg = dano sofrido, kills, akSum/akN = tempo alertar→abater.</summary>
    [System.Serializable]
    public class QuestProgress
    {
        public string id; public int progress; public bool done;
        public int v, kills, akN;
        public float time, dmg, akSum;
    }

    /// <summary>Melhor nota já tirada numa missão (grade: 0 = F … 6 = S+).</summary>
    [System.Serializable]
    public class QuestGradeRecord { public string id; public int grade; public float time; }

    /// <summary>Contadores para conquistas (todos opcionais em saves antigos).</summary>
    [System.Serializable]
    public class GuildStats
    {
        public int kills, bosses, goblins, floors, floorsNoDmg, deaths, bestCombo, chains, gradeS, gradeSPlus, collected;
    }

    [System.Serializable]
    public class OwnedPet { public int uid; public string petId, name; public int level = 1, xp; }

    [System.Serializable]
    public class GuildProfile
    {
        public string heroName = "";
        public int points, rank, storyStep, totalQuests, nextPetUid = 1, activePet = -1;
        public string title = "Novato";
        public string dailyDate = "";
        public bool trialAnnounced;
        public List<string> dailyOffer = new();
        public List<string> dailyDone = new();
        public List<string> completed = new();
        public List<QuestProgress> active = new();
        public List<OwnedPet> pets = new();
        // [Progressão] campos novos (opcionais: saves antigos carregam com os padrões)
        public GuildStats stats = new();
        public List<QuestGradeRecord> bestGrades = new();
        public List<int> gradeCounts = new();      // índice = nota (0 = F … 6 = S+)
        public List<string> achievements = new();  // ids desbloqueados
        public List<string> collected = new();     // ids de colecionáveis obtidos
        public List<string> lore = new();          // páginas do Códex liberadas
        public List<string> loreRead = new();      // páginas já abertas no Códex
        public List<string> chainsDone = new();    // cadeias de sequência concluídas
        public string chainOffer = "";             // etapa de sequência disponível no quadro ("" = nenhuma)
    }

    /// <summary>Estado da Guilda (rank, pontos, missões, pets). Salvo à parte do save principal.</summary>
    public static class GuildState
    {
        public static GuildProfile G = new();
        public static event System.Action Changed;

        static bool dirty;
        static float saveClock;

        static string SavePath => Path.Combine(Application.persistentDataPath, "drakantus_guild.json");
        static string MainSavePath => Path.Combine(Application.persistentDataPath, "drakantus_save.json");

        public static void Emit() => Changed?.Invoke();

        /// <summary>Marca para salvar em alguns segundos (progresso de missão, XP do pet).</summary>
        public static void MarkDirty() { dirty = true; }

        public static void Save()
        {
            dirty = false;
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(G, true)); }
            catch (System.Exception e) { Debug.LogWarning("[Drakantus] Não foi possível salvar a Guilda: " + e.Message); }
        }

        public static bool Load()
        {
            try
            {
                if (!File.Exists(SavePath)) return false;
                var g = JsonUtility.FromJson<GuildProfile>(File.ReadAllText(SavePath));
                if (g == null) return false;
                G = g;
                Sanitize();
                return true;
            }
            catch (System.Exception e) { Debug.LogWarning("[Drakantus] Save da Guilda inválido: " + e.Message); return false; }
        }

        public static void Reset()
        {
            G = new GuildProfile { heroName = GameState.P != null ? GameState.P.playerName : "" };
            var r0 = GuildData.Rank(0);
            if (r0 != null && !string.IsNullOrEmpty(r0.title)) G.title = r0.title;
        }

        static void Sanitize()
        {
            if (G.dailyOffer == null) G.dailyOffer = new List<string>();
            if (G.dailyDone == null) G.dailyDone = new List<string>();
            if (G.completed == null) G.completed = new List<string>();
            if (G.active == null) G.active = new List<QuestProgress>();
            if (G.pets == null) G.pets = new List<OwnedPet>();
            if (G.stats == null) G.stats = new GuildStats();
            if (G.bestGrades == null) G.bestGrades = new List<QuestGradeRecord>();
            if (G.gradeCounts == null) G.gradeCounts = new List<int>();
            if (G.achievements == null) G.achievements = new List<string>();
            if (G.collected == null) G.collected = new List<string>();
            if (G.lore == null) G.lore = new List<string>();
            if (G.loreRead == null) G.loreRead = new List<string>();
            if (G.chainsDone == null) G.chainsDone = new List<string>();
            if (G.chainOffer == null) G.chainOffer = "";
            if (G.chainOffer.Length > 0 && GuildData.Quest(G.chainOffer) == null) G.chainOffer = "";
            G.bestGrades.RemoveAll(b => b == null || string.IsNullOrEmpty(b.id));
            G.rank = Mathf.Clamp(G.rank, 0, Mathf.Max(0, GuildData.Ranks.Count - 1));
            G.active.RemoveAll(a => a == null || Quests.Def(a.id) == null);
            G.pets.RemoveAll(p => p == null || GuildData.Pet(p.petId) == null);
            foreach (var p in G.pets) p.level = Mathf.Clamp(p.level, 1, Pets.MaxLevel);
            if (G.activePet >= 0 && G.pets.Find(p => p.uid == G.activePet) == null) G.activePet = -1;
        }

        /// <summary>Chamado pelo Game.StartGame (depois de GameState.NewGame/Load). Um herói novo zera a Guilda.</summary>
        public static void OnStartGame()
        {
            GuildData.Load();
            bool fresh = IsFreshHero();
            if (fresh || !Load() || G.heroName != GameState.P.playerName) Reset();
            G.heroName = GameState.P.playerName;
            Quests.RefreshDaily();
            Lore.CheckUnlocks(false);   // [Progressão] páginas iniciais/por rank (sem aviso ao carregar)
            Save();
            Emit();
        }

        /// <summary>true se o perfil atual não é o que está no disco (= acabou de fazer "Novo jogo").</summary>
        static bool IsFreshHero()
        {
            try
            {
                if (!File.Exists(MainSavePath)) return true;
                var d = JsonUtility.FromJson<Profile>(File.ReadAllText(MainSavePath));
                if (d == null) return true;
                var p = GameState.P;
                return d.playerName != p.playerName || d.level != p.level || d.xp != p.xp || d.coins != p.coins;
            }
            catch (System.Exception) { return false; }
        }

        /// <summary>Chamado todo frame pelo GuildHud: salva o que ficou pendente.</summary>
        public static void Tick(float dt)
        {
            if (!dirty) return;
            saveClock += dt;
            if (saveClock < 3f) return;
            saveClock = 0f;
            Save();
        }

        public static void Flush() { if (dirty) Save(); }
    }
}
