using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Conquista (Resources/Data/achievements.json). stat = contador medido (ver Achievements.Value);
    /// target = valor a atingir (0 = "todos", resolvido pelo total do jogo). Recompensa: coins, xp, item.</summary>
    [System.Serializable]
    public class AchievementDef
    {
        public string id, name, desc, stat, item, color;
        public int target, coins, xp, order;
        public bool hidden;
    }

    [System.Serializable] class AchievementList { public AchievementDef[] achievements; }

    /// <summary>
    /// Conquistas da Guilda: contadores em GuildState.G.stats (abates, chefes, Goblin de Ouro, andares sem dano,
    /// combo, notas...) + valores lidos na hora (rank, nível, pets, colecionáveis, páginas do Códex).
    /// Ganchos: Quests.OnEnemyKilled → OnKill, Quests.OnFloorCleared → OnFloorCleared, Pets.OnTravel → OnTravel,
    /// QuestGrading (vida caiu) → OnDamaged, Player.Died (por instância) e combo por polling.
    /// Ao desbloquear: recompensa + aviso animado no topo (HUD.AchievementPopup, som "achievement").
    /// </summary>
    public static class Achievements
    {
        public static readonly List<AchievementDef> All = new();
        static readonly Dictionary<string, AchievementDef> byId = new();
        static bool loaded;

        static GuildProfile G => GuildState.G;

        // estado transitório
        static Player watched;
        static bool floorRun, floorDamaged;
        static float checkClock;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            var ta = Resources.Load<TextAsset>("Data/achievements");
            if (ta == null) { Debug.LogWarning("[Drakantus] Faltando Resources/Data/achievements.json"); return; }
            AchievementList l = null;
            try { l = JsonUtility.FromJson<AchievementList>(ta.text); }
            catch (System.Exception ex) { Debug.LogError("[Drakantus] Erro lendo achievements.json: " + ex.Message); }
            if (l == null || l.achievements == null) return;
            foreach (var a in l.achievements)
            {
                if (a == null || string.IsNullOrEmpty(a.id) || byId.ContainsKey(a.id)) continue;
                byId[a.id] = a;
                All.Add(a);
            }
            All.Sort((x, y) => x.order.CompareTo(y.order));
        }

        public static AchievementDef Def(string id) { Load(); return id != null && byId.TryGetValue(id, out var d) ? d : null; }
        public static bool Unlocked(string id) => G.achievements.Contains(id);
        public static int UnlockedCount { get { Load(); int n = 0; foreach (var a in All) if (Unlocked(a.id)) n++; return n; } }
        public static int Total { get { Load(); return All.Count; } }

        // ------------------------------------------------------------------ valores
        /// <summary>Valor atual do contador da conquista.</summary>
        public static int Value(string stat)
        {
            var s = G.stats;
            switch (stat)
            {
                case "kills": return s.kills;
                case "bosses": return s.bosses;
                case "goblins": return s.goblins;
                case "floors": return s.floors;
                case "floorsNoDmg": return s.floorsNoDmg;
                case "deaths": return s.deaths;
                case "combo": return s.bestCombo;
                case "chains": return s.chains;
                case "chainsAll": return G.chainsDone.Count;
                case "gradeS": return s.gradeS;
                case "gradeSPlus": return s.gradeSPlus;
                case "quests": return G.totalQuests;
                case "rank": return G.rank;
                case "level": return GameState.P != null ? GameState.P.level : 1;
                case "coins": return GameState.P != null ? GameState.P.coins : 0;
                case "pets": return G.pets.Count;
                case "petMax": { int n = 0; foreach (var p in G.pets) if (p.level >= Pets.MaxLevel) n++; return n; }
                case "collected": return G.collected.Count;
                case "stars": return Collectibles.CountKind("estrela", true);
                case "books": return Collectibles.CountKind("livro", true);
                case "oldcoins": return Collectibles.CountKind("moeda", true);
                case "lore": return Lore.UnlockedCount;
                case "achievements": return G.achievements.Count;
            }
            return 0;
        }

        /// <summary>Meta da conquista (target 0 = todos do jogo).</summary>
        public static int Target(AchievementDef d)
        {
            if (d == null) return 1;
            if (d.target > 0) return d.target;
            switch (d.stat)
            {
                case "stars": return Mathf.Max(1, Collectibles.CountKind("estrela", false));
                case "books": return Mathf.Max(1, Collectibles.CountKind("livro", false));
                case "oldcoins": return Mathf.Max(1, Collectibles.CountKind("moeda", false));
                case "collected": return Mathf.Max(1, Collectibles.Total);
                case "lore": return Mathf.Max(1, Lore.Total);
                case "pets": GuildData.Load(); return Mathf.Max(1, GuildData.PetOrder.Count);
                case "chainsAll": return Mathf.Max(1, Quests.ChainIds().Count);
                case "rank": return Mathf.Max(1, Ranks.Count - 1);
                case "achievements": return Mathf.Max(1, Total - 1);
            }
            return 1;
        }

        public static float Progress(AchievementDef d, out int have, out int need)
        {
            need = Target(d);
            have = Mathf.Min(Value(d.stat), need);
            if (Unlocked(d.id)) have = need;
            return Mathf.Clamp01(have / (float)Mathf.Max(1, need));
        }

        public static string RewardText(AchievementDef d, bool rich = true)
        {
            if (d == null) return "";
            var parts = new List<string>();
            if (d.coins > 0) parts.Add(d.coins + " moedas");
            if (d.xp > 0) parts.Add(d.xp + " XP");
            var it = GameData.Item(d.item);
            if (it != null)
            {
                string nm = it.name;
                if (rich && it.rarity != "comum") nm = "<color=#" + ColorUtility.ToHtmlStringRGB(GameData.RarityColor(it)) + ">" + nm + "</color>";
                parts.Add(nm);
            }
            return parts.Count > 0 ? string.Join("  ·  ", parts) : "—";
        }

        // ------------------------------------------------------------------ verificação
        /// <summary>Desbloqueia tudo que já atingiu a meta.</summary>
        public static void Check()
        {
            Load();
            if (GameState.P == null) return;
            bool any = false;
            for (int i = 0; i < All.Count; i++)
            {
                var d = All[i];
                if (Unlocked(d.id)) continue;
                if (Value(d.stat) < Target(d)) continue;
                Unlock(d);
                any = true;
            }
            if (any)
            {
                // conquistas que contam conquistas
                for (int i = 0; i < All.Count; i++)
                {
                    var d = All[i];
                    if (d.stat == "achievements" && !Unlocked(d.id) && Value(d.stat) >= Target(d)) Unlock(d);
                }
                GameState.Save();
                GuildState.Save();
                GuildState.Emit();
            }
        }

        static void Unlock(AchievementDef d)
        {
            G.achievements.Add(d.id);
            if (d.coins > 0) GameState.AddCoins(d.coins);
            if (d.xp > 0) GameState.AddXp(d.xp);
            if (GameData.Item(d.item) != null) GameState.Grant(d.item);
            if (HUD.I != null) HUD.I.AchievementPopup(d);   // o aviso toca "achievement" ao aparecer
            else { Sfx.Play("achievement"); GameState.Notify("Conquista desbloqueada: " + d.name); }
        }

        // ------------------------------------------------------------------ ganchos
        public static void OnKill(string enemyId, bool boss)
        {
            G.stats.kills++;
            if (boss) G.stats.bosses++;
            if (enemyId == "goblin_ouro") G.stats.goblins++;
            GuildState.MarkDirty();
            Lore.OnKill(enemyId);
            Check();
        }

        public static void OnDamaged(float amount)
        {
            if (amount > 0f) floorDamaged = true;
        }

        /// <summary>Chamado pelo Pets.OnTravel (a cada mapa novo).</summary>
        public static void OnTravel()
        {
            var g = Game.I;
            floorRun = g != null && g.InDungeon && g.level != null && g.level.floor > 0;
            floorDamaged = false;
            QuestGrading.OnTravel();
        }

        public static void OnFloorCleared(int floor)
        {
            G.stats.floors++;
            if (floorRun && !floorDamaged)
            {
                G.stats.floorsNoDmg++;
                GameState.Notify("Andar " + floor + " concluído sem sofrer dano!");
            }
            floorRun = false;
            GuildState.MarkDirty();
            Check();
        }

        static void OnPlayerDied(Player p)
        {
            G.stats.deaths++;
            floorDamaged = true;
            GuildState.MarkDirty();
            Check();
        }

        /// <summary>Loop (Quests.Tick): assina o Player.Died, mede o melhor combo e confere metas a cada segundo.</summary>
        public static void Tick(float dt)
        {
            var g = Game.I;
            if (g == null || !g.Started) return;
            var p = g.player;
            if (p != watched)
            {
                if (watched != null) watched.Died -= OnPlayerDied;
                watched = p;
                if (p != null) p.Died += OnPlayerDied;
            }
            if (p != null && p.combo != null && p.combo.Combo > G.stats.bestCombo)
            {
                G.stats.bestCombo = p.combo.Combo;
                GuildState.MarkDirty();
                if (G.stats.bestCombo % 5 == 0) Check();
            }
            checkClock -= Time.unscaledDeltaTime;
            if (checkClock <= 0f)
            {
                checkClock = 1f;
                Lore.CheckUnlocks(true);
                Check();
            }
        }
    }
}
