using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Missões da Guilda (quadro do Oren): diárias (renovam a cada dia real), caçadas (1x por dia),
    /// história (sequência fixa) e Prova de Promoção (gerada a partir de ranks.json).
    /// Máximo de 3 ativas (a prova não conta). Ao concluir, volte ao quadro/Oren para entregar.
    /// </summary>
    public static class Quests
    {
        public const int MaxActive = 3;
        public const int DailyOffer = 4;
        public const string PromoPrefix = "promo_";
        /// <summary>Chance de surgir uma Missão de Sequência ao entregar uma missão sem ter sofrido dano.</summary>
        public const float ChainChance = 0.05f;

        static readonly Dictionary<string, QuestDef> promoCache = new();
        static readonly Dictionary<string, float> lastCd = new();
        static Player watched;
        static float dateClock;

        static GuildProfile G => GuildState.G;
        public static string Today => System.DateTime.Now.ToString("yyyy-MM-dd");

        // ------------------------------------------------------------------ definições
        public static QuestDef Def(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id.StartsWith(PromoPrefix)) return PromoDef(id);
            return GuildData.Quest(id);
        }

        static QuestDef PromoDef(string id)
        {
            if (promoCache.TryGetValue(id, out var q)) return q;
            if (!int.TryParse(id.Substring(PromoPrefix.Length), out int r)) return null;
            if (r <= 0 || r >= GuildData.Ranks.Count) return null;
            var rd = GuildData.Ranks[r];
            var t = rd.trial ?? new RankTrial { goal = "kill", target = "any", count = 20 };
            q = new QuestDef
            {
                id = id, type = "promo",
                title = string.IsNullOrEmpty(t.title) ? "Prova de Promoção: Rank " + rd.name : t.title,
                desc = string.IsNullOrEmpty(t.desc) ? "Prove seu valor para subir ao Rank " + rd.name + "." : t.desc,
                goal = string.IsNullOrEmpty(t.goal) ? "kill" : t.goal,
                target = string.IsNullOrEmpty(t.target) ? "any" : t.target,
                count = Mathf.Max(1, t.count), coins = t.coins, xp = t.xp, points = 0, minRank = r - 1,
                items = new string[0]
            };
            promoCache[id] = q;
            return q;
        }

        public static string TypeName(QuestDef d)
        {
            switch (d != null ? d.type : "")
            {
                case "daily": return "DIÁRIA";
                case "hunt": return "CAÇADA";
                case "story": return "HISTÓRIA";
                case "promo": return "PROVA DE PROMOÇÃO";
                case "chain": return "SEQUÊNCIA " + d.chainStep + "/" + ChainLength(d.chain);
            }
            return "MISSÃO";
        }

        public static Color TypeColor(QuestDef d)
        {
            switch (d != null ? d.type : "")
            {
                case "daily": return U.Hex("3f8fe8");
                case "hunt": return U.Hex("e04a3a");
                case "story": return U.Hex("3fae5a");
                case "promo": return U.Hex("f2b632");
                case "chain": return U.Hex("e8a820");
            }
            return U.Hex("a0a0a0");
        }

        static string EnemyName(string id)
        {
            var e = GameData.Enemy(id);
            return e != null ? e.name : id;
        }

        public static string GoalText(QuestDef d)
        {
            if (d == null) return "";
            bool any = string.IsNullOrEmpty(d.target) || d.target == "any";
            switch (d.goal)
            {
                case "kill": return "Derrote " + d.count + "x " + (any ? "criaturas da Torre" : EnemyName(d.target));
                case "boss": return any ? "Derrote " + d.count + " chefe" + (d.count > 1 ? "s" : "") : "Derrote " + EnemyName(d.target) + (d.count > 1 ? " " + d.count + " vezes" : "");
                case "collect":
                    if (d.target == "coin") return "Recolha " + d.count + " moedas do chão";
                    if (d.target == "item") return "Recolha " + d.count + " itens do chão";
                    return "Recolha " + d.count + " tesouros do chão";
                case "skill": return "Use habilidades " + d.count + " vezes";
                case "floor": return any ? "Conclua " + d.count + " andar" + (d.count > 1 ? "es" : "") + " da Torre" : "Conclua o Andar " + d.target + (d.count > 1 ? " " + d.count + " vezes" : "");
            }
            return d.desc ?? "";
        }

        public static string RewardText(QuestDef d, bool rich = true)
        {
            if (d == null) return "";
            var parts = new List<string>();
            if (d.coins > 0) parts.Add(d.coins + " moedas");
            if (d.xp > 0) parts.Add(d.xp + " XP");
            if (d.points > 0) parts.Add((rich ? "<b>" : "") + d.points + " PG" + (rich ? "</b>" : ""));
            if (d.type == "promo") parts.Add((rich ? "<b>" : "") + "Promoção de Rank" + (rich ? "</b>" : ""));
            if (d.items != null)
            {
                var counts = new Dictionary<string, int>();
                var order = new List<string>();
                foreach (var it in d.items)
                {
                    if (GameData.Item(it) == null) continue;
                    if (!counts.ContainsKey(it)) { counts[it] = 0; order.Add(it); }
                    counts[it]++;
                }
                foreach (var it in order)
                {
                    var idf = GameData.Item(it);
                    string nm = (counts[it] > 1 ? counts[it] + "x " : "") + idf.name;
                    if (rich && idf.rarity != "comum") nm = "<color=#" + ColorUtility.ToHtmlStringRGB(GameData.RarityColor(idf)) + ">" + nm + "</color>";
                    parts.Add(nm);
                }
            }
            return string.Join("  ·  ", parts);
        }

        // ------------------------------------------------------------------ diárias
        public static void RefreshDaily()
        {
            GuildData.Load();
            string today = Today;
            if (G.dailyDate == today && G.dailyOffer.Count > 0) return;
            bool newDay = G.dailyDate != today;
            G.dailyDate = today;
            if (newDay)
            {
                G.dailyDone.Clear();
                // diárias/caçadas não concluídas do dia anterior expiram
                int expired = G.active.RemoveAll(a => { var d = Def(a.id); return d != null && (d.type == "daily" || d.type == "hunt") && !a.done; });
                if (expired > 0 && !string.IsNullOrEmpty(G.heroName)) GameState.Notify("Novo dia! As missões diárias foram renovadas.");
            }
            var pool = new List<QuestDef>();
            foreach (var q in GuildData.QuestOrder) if (q.type == "daily") pool.Add(q);
            int seed = 17;
            foreach (char c in today + (G.heroName ?? "")) seed = seed * 31 + c;
            var rng = new System.Random(seed);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
            }
            G.dailyOffer.Clear();
            // primeiro as que o rank atual permite
            foreach (var q in pool) if (G.dailyOffer.Count < DailyOffer && q.minRank <= G.rank) G.dailyOffer.Add(q.id);
            foreach (var q in pool) if (G.dailyOffer.Count < DailyOffer && !G.dailyOffer.Contains(q.id)) G.dailyOffer.Add(q.id);
            GuildState.MarkDirty();
            GuildState.Emit();
        }

        // ------------------------------------------------------------------ consultas
        public static QuestProgress Active(string id) => G.active.Find(a => a.id == id);
        public static bool IsActive(string id) => Active(id) != null;

        public static int ActiveCount()
        {
            int n = 0;
            foreach (var a in G.active) { var d = Def(a.id); if (d != null && d.type != "promo" && d.type != "chain") n++; }
            return n;
        }

        public static int ReadyCount()
        {
            int n = 0;
            foreach (var a in G.active) if (a.done) n++;
            return n;
        }

        /// <summary>Próxima missão de história (ou null quando a campanha acabou).</summary>
        public static QuestDef NextStory()
        {
            var story = new List<QuestDef>();
            foreach (var q in GuildData.QuestOrder) if (q.type == "story") story.Add(q);
            story.Sort((a, b) => a.order.CompareTo(b.order));
            foreach (var q in story) if (!G.completed.Contains(q.id)) return q;
            return null;
        }

        /// <summary>Papéis disponíveis no quadro (ainda não aceitos).</summary>
        public static List<QuestDef> Available()
        {
            var l = new List<QuestDef>();
            if (Ranks.TrialReady)
            {
                var p = Def(PromoPrefix + (G.rank + 1));
                if (p != null && !IsActive(p.id)) l.Add(p);
            }
            if (!string.IsNullOrEmpty(G.chainOffer))
            {
                var ch = GuildData.Quest(G.chainOffer);
                if (ch != null && !IsActive(ch.id)) l.Add(ch);
            }
            var s = NextStory();
            if (s != null && !IsActive(s.id) && s.minRank <= G.rank) l.Add(s);
            foreach (var id in G.dailyOffer)
            {
                var q = GuildData.Quest(id);
                if (q == null || IsActive(id) || G.dailyDone.Contains(id) || q.minRank > G.rank) continue;
                l.Add(q);
            }
            foreach (var q in GuildData.QuestOrder)
            {
                if (q.type != "hunt" || IsActive(q.id) || G.dailyDone.Contains(q.id) || q.minRank > G.rank) continue;
                l.Add(q);
            }
            return l;
        }

        /// <summary>Missões bloqueadas pelo rank (para mostrar "Rank X necessário").</summary>
        public static List<QuestDef> LockedByRank()
        {
            var l = new List<QuestDef>();
            foreach (var q in GuildData.QuestOrder)
            {
                if (q.minRank <= G.rank || q.type == "story" || q.type == "chain") continue;
                if (q.type == "daily" && !G.dailyOffer.Contains(q.id)) continue;
                l.Add(q);
            }
            return l;
        }

        // ------------------------------------------------------------------ ações
        public static bool Accept(string id)
        {
            var d = Def(id);
            if (d == null || IsActive(id)) return false;
            if (d.type != "promo" && d.type != "chain" && ActiveCount() >= MaxActive)
            {
                GameState.Notify("Você já tem " + MaxActive + " missões ativas. Entregue ou abandone uma.");
                return false;
            }
            if (d.minRank > G.rank && d.type != "promo" && d.type != "chain") { GameState.Notify("Rank insuficiente para esta missão."); return false; }
            var qp = new QuestProgress { id = id, progress = 0, done = false };
            QuestGrading.OnAccepted(qp);   // [Progressão] começa a medir tempo/dano/abates para a nota
            G.active.Add(qp);
            GameState.Notify("Missão aceita: " + d.title);
            GuildState.Save();
            GuildState.Emit();
            return true;
        }

        public static void Abandon(string id)
        {
            var a = Active(id);
            if (a == null) return;
            var d = Def(id);
            G.active.Remove(a);
            GameState.Notify("Missão abandonada: " + (d != null ? d.title : id));
            GuildState.Save();
            GuildState.Emit();
        }

        public static bool TurnIn(string id)
        {
            var a = Active(id);
            var d = Def(id);
            if (a == null || d == null || !a.done) return false;
            G.active.Remove(a);

            // [Progressão] nota da missão multiplica a recompensa
            int grade = QuestGrading.Grade(a, d);
            float mult = QuestGrading.Multiplier(grade);
            bool flawless = QuestGrading.Flawless(a);
            int coins = QuestGrading.Scale(d.coins, mult), xp = QuestGrading.Scale(d.xp, mult), pts = QuestGrading.Scale(d.points, mult);
            if (coins > 0) GameState.AddCoins(coins);
            if (xp > 0) GameState.AddXp(xp);
            if (d.items != null)
                foreach (var it in d.items)
                    if (GameData.Item(it) != null) GameState.Grant(it);
            string bonus = grade >= QuestGrading.SPlus ? QuestGrading.GrantBonusItem() : null;
            if (pts > 0) G.points += pts;
            G.totalQuests++;
            switch (d.type)
            {
                case "story": if (!G.completed.Contains(d.id)) G.completed.Add(d.id); G.storyStep++; break;
                case "daily": case "hunt": if (!G.dailyDone.Contains(d.id)) G.dailyDone.Add(d.id); break;
            }
            var prevBest = QuestGrading.Best(d.id);
            bool newBest = prevBest == null || grade > prevBest.grade;
            QuestGrading.Record(d, a, grade);

            var parts = new List<string>();
            if (coins > 0) parts.Add(coins + " moedas");
            if (xp > 0) parts.Add(xp + " XP");
            if (pts > 0) parts.Add(pts + " PG");
            if (bonus != null) parts.Add("bônus: " + bonus);
            string rewards = string.Join("  ·  ", parts);
            string multTxt = Mathf.Approximately(mult, 1f) ? "" : "  (x" + mult.ToString("0.##") + ")";
            GameState.Notify("Missão entregue: " + d.title + " — Nota " + QuestGrading.Letter(grade) + "  (" + rewards + ")");
            Sfx.Play("coin");
            if (d.type == "promo") Ranks.Promote();
            else Ranks.CheckPromotion();
            string stats = a.v > 0
                ? "Tempo " + QuestGrading.FormatTime(a.time) + " / meta " + QuestGrading.FormatTime(QuestGrading.TargetTime(d)) +
                  "   ·   Dano sofrido " + Mathf.RoundToInt(a.dmg) + "   ·   Abates " + a.kills
                : "Missão aceita antes do sistema de notas";
            if (HUD.I != null) HUD.I.GradeStamp(d, grade, rewards + multTxt, stats, newBest);

            // [Progressão] Missões de Sequência
            if (d.type == "chain") AdvanceChain(d);
            else if (flawless && d.type != "promo") TryChainOffer();

            GameState.Save();
            GuildState.Save();
            GuildState.Emit();
            Achievements.Check();
            return true;
        }

        // ------------------------------------------------------------------ missões de sequência
        /// <summary>Ids das cadeias de sequência existentes no quests.json.</summary>
        public static List<string> ChainIds()
        {
            GuildData.Load();
            var l = new List<string>();
            foreach (var q in GuildData.QuestOrder)
                if (q.type == "chain" && !string.IsNullOrEmpty(q.chain) && !l.Contains(q.chain)) l.Add(q.chain);
            return l;
        }

        public static QuestDef ChainStep(string chain, int step)
        {
            if (string.IsNullOrEmpty(chain)) return null;
            GuildData.Load();
            foreach (var q in GuildData.QuestOrder)
                if (q.type == "chain" && q.chain == chain && q.chainStep == step) return q;
            return null;
        }

        public static int ChainLength(string chain)
        {
            int n = 0;
            while (ChainStep(chain, n + 1) != null) n++;
            return Mathf.Max(1, n);
        }

        static void AdvanceChain(QuestDef d)
        {
            var next = ChainStep(d.chain, d.chainStep + 1);
            if (next != null)
            {
                G.chainOffer = next.id;
                GameState.Notify("Sequência: a etapa " + next.chainStep + " está no quadro — " + next.title);
                Sfx.Play("item_rare");
                return;
            }
            G.chainOffer = "";
            if (!G.chainsDone.Contains(d.chain)) G.chainsDone.Add(d.chain);
            G.stats.chains++;
            GameState.Notify("Sequência concluída! A Guilda vai cantar sobre isso.");
            Sfx.Play("item_legendary");
        }

        /// <summary>5% de chance (entrega sem dano) de surgir a 1ª etapa de uma cadeia rara no quadro.</summary>
        static void TryChainOffer()
        {
            if (!string.IsNullOrEmpty(G.chainOffer)) return;
            if (Random.value >= ChainChance) return;
            OfferRandomChain();
        }

        /// <summary>Coloca a 1ª etapa de uma cadeia (de preferência ainda não concluída) no quadro. Também serve para testes.</summary>
        public static bool OfferRandomChain()
        {
            if (!string.IsNullOrEmpty(G.chainOffer)) return false;
            var ids = ChainIds();
            var fresh = ids.FindAll(c => !G.chainsDone.Contains(c));
            var pool = fresh.Count > 0 ? fresh : ids;
            if (pool.Count == 0) return false;
            var first = ChainStep(pool[Random.Range(0, pool.Count)], 1);
            if (first == null) return false;
            G.chainOffer = first.id;
            GameState.Notify("Uma rara Missão de Sequência apareceu no quadro da Guilda!");
            Sfx.Play("item_legendary");
            GuildState.Save();
            GuildState.Emit();
            return true;
        }

        public static int TurnInAll()
        {
            var ids = new List<string>();
            foreach (var a in G.active) if (a.done) ids.Add(a.id);
            int n = 0;
            foreach (var id in ids) if (TurnIn(id)) n++;
            return n;
        }

        // ------------------------------------------------------------------ progresso (ganchos do jogo)
        /// <summary>Chamado pelo Enemy ao morrer.</summary>
        public static void OnEnemyKilled(string enemyId, bool boss)
        {
            Pets.OnHeroKill(boss);
            QuestGrading.OnKill();                    // [Progressão]
            Achievements.OnKill(enemyId, boss);       // [Progressão] contadores + páginas "kill:"
            if (G.active.Count == 0) return;
            Advance(d =>
                (d.goal == "kill" && (d.target == "any" || d.target == enemyId)) ||
                (d.goal == "boss" && boss && (d.target == "any" || d.target == enemyId)), 1);
        }

        /// <summary>what: "coin" | "item" (loot recolhido do chão).</summary>
        public static void OnItemCollected(string what)
        {
            if (G.active.Count == 0) return;
            Advance(d => d.goal == "collect" && (d.target == "any" || d.target == what), 1);
        }

        public static void OnSkillUsed()
        {
            if (G.active.Count == 0) return;
            Advance(d => d.goal == "skill", 1);
        }

        public static void OnFloorCleared(int floor)
        {
            Achievements.OnFloorCleared(floor);       // [Progressão]
            if (G.active.Count == 0) return;
            string f = floor.ToString();
            Advance(d => d.goal == "floor" && (d.target == "any" || d.target == f), 1);
        }

        static void Advance(System.Func<QuestDef, bool> match, int amount)
        {
            bool any = false;
            foreach (var a in G.active)
            {
                if (a.done) continue;
                var d = Def(a.id);
                if (d == null || !match(d)) continue;
                int before = a.progress;
                a.progress = Mathf.Min(d.count, a.progress + amount);
                any = true;
                if (a.progress >= d.count)
                {
                    a.done = true;
                    GameState.Notify("Missão concluída: " + d.title + "! Entregue no quadro da Guilda.");
                    Sfx.Play("skill_unlock");
                    GuildState.Save();
                }
                else if (Milestone(before, a.progress, d.count))
                    GameState.Notify(d.title + ": " + a.progress + "/" + d.count);
            }
            if (any) { GuildState.MarkDirty(); GuildState.Emit(); }
        }

        /// <summary>Evita inundar de avisos: até 5 passos avisa sempre, depois só a cada 25%.</summary>
        static bool Milestone(int before, int now, int total)
        {
            if (total <= 5) return true;
            int qb = before * 4 / total, qn = now * 4 / total;
            return qn > qb;
        }

        // ------------------------------------------------------------------ loop (chamado pelo GuildHud)
        public static void Tick(float dt)
        {
            dateClock -= dt;
            if (dateClock <= 0f)
            {
                dateClock = 10f;
                if (G.dailyDate != Today && Game.I != null && Game.I.Started) RefreshDaily();
            }
            WatchSkills();
            QuestGrading.Tick(dt);       // [Progressão] tempo/dano/abates das missões ativas
            Achievements.Tick(dt);       // [Progressão] combo, Player.Died e metas
        }

        /// <summary>Detecta o uso de habilidades sem mexer no PlayerSkills: a recarga de um slot "salta" para cima.</summary>
        static void WatchSkills()
        {
            var p = Game.I != null ? Game.I.player : null;
            if (p == null) { watched = null; return; }
            if (p != watched) { watched = p; lastCd.Clear(); foreach (var kv in p.cooldowns) lastCd[kv.Key] = kv.Value; return; }
            int used = 0;
            foreach (var kv in p.cooldowns)
            {
                float prev;
                bool had = lastCd.TryGetValue(kv.Key, out prev);
                if ((!had && kv.Value > 0.05f) || (had && kv.Value > prev + 0.05f)) used++;
            }
            if (used > 0 || p.cooldowns.Count != lastCd.Count)
            {
                lastCd.Clear();
                foreach (var kv in p.cooldowns) lastCd[kv.Key] = kv.Value;
            }
            else
            {
                // atualiza sem alocar
                foreach (var kv in p.cooldowns) tmpKeys.Add(kv.Key);
                foreach (var k in tmpKeys) lastCd[k] = p.cooldowns[k];
                tmpKeys.Clear();
            }
            for (int i = 0; i < used; i++) OnSkillUsed();
        }

        static readonly List<string> tmpKeys = new();
    }
}
