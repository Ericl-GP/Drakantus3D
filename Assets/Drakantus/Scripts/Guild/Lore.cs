using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Página do Códex (Resources/Data/lore.json). unlock: "start" | "book" (Livro de Lore coletado) |
    /// "kill:&lt;idInimigo&gt;" | "rank:&lt;n&gt;" | "pets:&lt;n&gt;" | "stars:&lt;n&gt;" | "goblins:&lt;n&gt;".</summary>
    [System.Serializable]
    public class LoreDef
    {
        public string id, chapter, title, text, unlock, hint;
        public int order;
    }

    [System.Serializable] class LoreList { public LoreDef[] pages; }

    /// <summary>
    /// Lore do mundo (Códex). Páginas liberadas por colecionáveis (Livros de Lore), chefes, rank, pets e
    /// Fragmentos de Estrela. Liberadas em GuildState.G.lore; lidas em G.loreRead.
    /// </summary>
    public static class Lore
    {
        public static readonly List<LoreDef> Pages = new();
        static readonly Dictionary<string, LoreDef> byId = new();
        static bool loaded;

        static GuildProfile G => GuildState.G;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            var ta = Resources.Load<TextAsset>("Data/lore");
            if (ta == null) { Debug.LogWarning("[Drakantus] Faltando Resources/Data/lore.json"); return; }
            LoreList l = null;
            try { l = JsonUtility.FromJson<LoreList>(ta.text); }
            catch (System.Exception ex) { Debug.LogError("[Drakantus] Erro lendo lore.json: " + ex.Message); }
            if (l == null || l.pages == null) return;
            foreach (var p in l.pages)
            {
                if (p == null || string.IsNullOrEmpty(p.id) || byId.ContainsKey(p.id)) continue;
                if (string.IsNullOrEmpty(p.unlock)) p.unlock = "book";
                byId[p.id] = p;
                Pages.Add(p);
            }
            Pages.Sort((a, b) => a.order.CompareTo(b.order));
        }

        public static LoreDef Def(string id) { Load(); return id != null && byId.TryGetValue(id, out var d) ? d : null; }
        public static int Total { get { Load(); return Pages.Count; } }
        public static bool IsUnlocked(string id) => G.lore.Contains(id);
        public static bool IsNew(string id) => IsUnlocked(id) && !G.loreRead.Contains(id);

        public static int UnlockedCount
        {
            get { Load(); int n = 0; foreach (var p in Pages) if (IsUnlocked(p.id)) n++; return n; }
        }

        public static int NewCount
        {
            get { Load(); int n = 0; foreach (var p in Pages) if (IsNew(p.id)) n++; return n; }
        }

        /// <summary>Libera uma página. notify = aviso + som de página.</summary>
        public static bool Unlock(string id, bool notify)
        {
            var d = Def(id);
            if (d == null || IsUnlocked(id)) return false;
            G.lore.Add(id);
            GuildState.MarkDirty();
            if (notify)
            {
                GameState.Notify("Nova página no Códex: \"" + d.title + "\"");
                Sfx.Play("page_turn");
            }
            GuildState.Emit();
            return true;
        }

        public static void MarkRead(string id)
        {
            if (!IsUnlocked(id) || G.loreRead.Contains(id)) return;
            G.loreRead.Add(id);
            GuildState.MarkDirty();
        }

        static int Arg(string unlock)
        {
            int i = unlock.IndexOf(':');
            int n;
            return i >= 0 && int.TryParse(unlock.Substring(i + 1), out n) ? n : 0;
        }

        static string ArgStr(string unlock)
        {
            int i = unlock.IndexOf(':');
            return i >= 0 ? unlock.Substring(i + 1) : "";
        }

        /// <summary>Confere as condições "start", "rank:", "pets:", "stars:", "goblins:" (barato; chamado a cada 1 s).</summary>
        public static void CheckUnlocks(bool notify)
        {
            Load();
            if (GameState.P == null) return;
            for (int i = 0; i < Pages.Count; i++)
            {
                var p = Pages[i];
                if (IsUnlocked(p.id)) continue;
                string u = p.unlock;
                bool ok = false;
                if (u == "start") ok = true;
                else if (u.StartsWith("rank:")) ok = G.rank >= Arg(u);
                else if (u.StartsWith("pets:")) ok = G.pets.Count >= Arg(u);
                else if (u.StartsWith("stars:")) ok = Collectibles.CountKind("estrela", true) >= Arg(u);
                else if (u.StartsWith("goblins:")) ok = G.stats.goblins >= Arg(u);
                if (ok) Unlock(p.id, notify && u != "start");
            }
        }

        /// <summary>Chamado pelo Achievements.OnKill: páginas "kill:&lt;id&gt;".</summary>
        public static void OnKill(string enemyId)
        {
            Load();
            if (string.IsNullOrEmpty(enemyId)) return;
            for (int i = 0; i < Pages.Count; i++)
            {
                var p = Pages[i];
                if (!IsUnlocked(p.id) && p.unlock.StartsWith("kill:") && ArgStr(p.unlock) == enemyId) Unlock(p.id, true);
            }
        }

        /// <summary>Como liberar (texto para páginas trancadas).</summary>
        public static string HintFor(LoreDef d)
        {
            if (d == null) return "";
            if (!string.IsNullOrEmpty(d.hint)) return d.hint;
            string u = d.unlock ?? "";
            if (u == "book") return "Encontre um Livro de Lore perdido nos andares da Torre.";
            if (u.StartsWith("kill:"))
            {
                var e = GameData.Enemy(ArgStr(u));
                return "Derrote " + (e != null ? e.name : "um inimigo lendário") + ".";
            }
            if (u.StartsWith("rank:")) return "Alcance o Rank " + Ranks.Letter(Arg(u)) + " da Guilda.";
            if (u.StartsWith("pets:")) return "Adote " + Arg(u) + " pet" + (Arg(u) > 1 ? "s" : "") + " com a Lumi.";
            if (u.StartsWith("stars:")) return "Reúna " + Arg(u) + " Fragmentos de Estrela.";
            if (u.StartsWith("goblins:")) return "Abata o Goblin de Ouro " + Arg(u) + " vez(es).";
            return "Continue explorando a Torre.";
        }
    }
}
