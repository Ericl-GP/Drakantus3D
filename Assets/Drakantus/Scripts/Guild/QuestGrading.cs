using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Nota por missão (F, D, C, B, A, S, S+). Mede, desde que a missão foi aceita até ela ser concluída:
    /// tempo de jogo (meta = QuestDef.targetTime ou padrão pelo objetivo/tipo), dano sofrido e velocidade de
    /// abate (abates por minuto e tempo médio entre o inimigo te ver e morrer). A recompensa é multiplicada
    /// pela nota (F 0,5x … S 1,5x, S+ 2x + item raro bônus). Guarda a melhor nota de cada missão.
    /// Estado salvo em QuestProgress (v, time, dmg, kills, akSum, akN) e GuildProfile (bestGrades, gradeCounts).
    /// </summary>
    public static class QuestGrading
    {
        public const int F = 0, D = 1, C = 2, B = 3, A = 4, S = 5, SPlus = 6;
        static readonly string[] letters = { "F", "D", "C", "B", "A", "S", "S+" };
        static readonly float[] mults = { 0.5f, 0.75f, 0.9f, 1f, 1.2f, 1.5f, 2f };
        static readonly string[] colors = { "8a8a8a", "b07a4a", "6aa8d8", "5fbf5a", "c86aff", "ffc83a", "ff5ad0" };

        static GuildProfile G => GuildState.G;

        // estado transitório (não salvo)
        static float lastHp = -1f;
        static Player watched;
        static readonly Dictionary<Enemy, float> alertAt = new();
        static readonly List<Enemy> tmp = new();

        public static string Letter(int g) => letters[Mathf.Clamp(g, 0, letters.Length - 1)];
        public static float Multiplier(int g) => mults[Mathf.Clamp(g, 0, mults.Length - 1)];
        public static Color GradeColor(int g) => U.Hex(colors[Mathf.Clamp(g, 0, colors.Length - 1)]);
        public static string GradeHex(int g) => colors[Mathf.Clamp(g, 0, colors.Length - 1)];
        public static int Scale(int v, float mult) => v <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(v * mult));

        // ------------------------------------------------------------------ meta de tempo
        /// <summary>Meta de tempo (s) da missão: targetTime do JSON ou padrão pelo objetivo e tipo.</summary>
        public static float TargetTime(QuestDef d)
        {
            if (d == null) return 300f;
            if (d.targetTime > 0f) return d.targetTime;
            int n = Mathf.Max(1, d.count);
            float t;
            switch (d.goal)
            {
                case "kill": t = 45f + n * 14f; break;
                case "boss": t = 240f * n; break;
                case "collect": t = 60f + n * 9f; break;
                case "skill": t = 60f + n * 7f; break;
                case "floor": t = 480f * n; break;
                default: t = 300f; break;
            }
            switch (d.type)
            {
                case "hunt": t *= 1.15f; break;
                case "story": t *= 1.2f; break;
                case "chain": t *= 1.1f; break;
            }
            return Mathf.Max(60f, t);
        }

        // ------------------------------------------------------------------ cálculo
        /// <summary>Pontuação 0..1 da missão (com as estatísticas atuais).</summary>
        public static float Score(QuestProgress a, QuestDef d, out float timeS, out float dmgS, out float killS)
        {
            timeS = dmgS = killS = 1f;
            if (a == null || d == null) return 0.5f;
            float ratio = a.time / TargetTime(d);
            timeS = Mathf.Clamp01(1f - (ratio - 0.6f) / 1.6f);               // 60% da meta = 1, 220% = 0
            float maxHp = Mathf.Max(1f, GameState.maxHp);
            dmgS = Mathf.Clamp01(1f - a.dmg / (maxHp * 2.5f));                // 2,5 barras de vida = 0
            bool hasKill = a.kills >= 3 && a.time > 1f;
            if (hasKill)
            {
                float kpm = a.kills / Mathf.Max(0.25f, a.time / 60f);
                float s = Mathf.Clamp01(kpm / 7f);                            // 7 abates/min = 1
                if (a.akN > 0)
                {
                    float avg = a.akSum / a.akN;
                    float s2 = Mathf.Clamp01(1f - (avg - 3f) / 14f);          // 3 s = 1, 17 s = 0
                    s = s * 0.5f + s2 * 0.5f;
                }
                killS = s;
                return timeS * 0.4f + dmgS * 0.35f + killS * 0.25f;
            }
            return timeS * 0.53f + dmgS * 0.47f;
        }

        /// <summary>Nota (0 = F … 6 = S+). Missões de saves antigos (sem rastreio) valem B.</summary>
        public static int Grade(QuestProgress a, QuestDef d)
        {
            if (a == null || d == null) return B;
            if (a.v <= 0) return B;
            float t, dm, k;
            float sc = Score(a, d, out t, out dm, out k);
            bool flawless = a.dmg <= 0.01f;
            if (flawless && sc >= 0.9f) return SPlus;
            if (sc >= 0.84f) return S;
            if (sc >= 0.72f) return A;
            if (sc >= 0.58f) return B;
            if (sc >= 0.44f) return C;
            if (sc >= 0.3f) return D;
            return F;
        }

        public static bool Flawless(QuestProgress a) => a != null && a.v > 0 && a.dmg <= 0.01f;

        // ------------------------------------------------------------------ histórico
        public static QuestGradeRecord Best(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return null;
            return G.bestGrades.Find(b => b.id == questId);
        }

        public static int CountOf(int grade)
        {
            return grade >= 0 && grade < G.gradeCounts.Count ? G.gradeCounts[grade] : 0;
        }

        /// <summary>Registra a nota (melhor nota por missão, contagem por nota, estatísticas de conquistas).</summary>
        public static void Record(QuestDef d, QuestProgress a, int grade)
        {
            if (d == null) return;
            while (G.gradeCounts.Count <= SPlus) G.gradeCounts.Add(0);
            G.gradeCounts[grade]++;
            if (grade >= S) G.stats.gradeS++;
            if (grade >= SPlus) G.stats.gradeSPlus++;
            var b = Best(d.id);
            if (b == null) G.bestGrades.Add(new QuestGradeRecord { id = d.id, grade = grade, time = a != null ? a.time : 0f });
            else if (grade > b.grade || (grade == b.grade && a != null && a.time < b.time)) { b.grade = grade; b.time = a != null ? a.time : b.time; }
        }

        /// <summary>Item bônus da nota S+: um equipamento raro aleatório (sem asas). Devolve o nome ou null.</summary>
        public static string GrantBonusItem()
        {
            var l = new List<string>();
            foreach (var it in GameData.ItemOrder)
                if (GameData.IsEquipSlotType(it.slot) && it.slot != "asa" && it.rarity == "raro") l.Add(it.id);
            if (l.Count == 0) return null;
            string id = l[Random.Range(0, l.Count)];
            GameState.Grant(id);
            var d = GameData.Item(id);
            return d != null ? d.name : id;
        }

        public static string FormatTime(float s)
        {
            int t = Mathf.Max(0, Mathf.RoundToInt(s));
            return (t / 60) + ":" + (t % 60).ToString("00");
        }

        // ------------------------------------------------------------------ rastreio (chamado pelo Quests)
        /// <summary>Uma missão acabou de ser aceita: zera as estatísticas dela.</summary>
        public static void OnAccepted(QuestProgress a)
        {
            if (a == null) return;
            a.v = 1; a.time = 0f; a.dmg = 0f; a.kills = 0; a.akSum = 0f; a.akN = 0;
        }

        /// <summary>Chamado pelo Quests.OnEnemyKilled.</summary>
        public static void OnKill()
        {
            foreach (var a in G.active) if (!a.done && a.v > 0) a.kills++;
        }

        /// <summary>Chamado a cada viagem (inimigos antigos foram destruídos).</summary>
        public static void OnTravel()
        {
            alertAt.Clear();
            lastHp = GameState.P != null ? GameState.P.hp : -1f;
        }

        /// <summary>Loop (Quests.Tick): tempo de missão, dano sofrido (vida que caiu) e tempo alertar→abater.</summary>
        public static void Tick(float dt)
        {
            var g = Game.I;
            if (g == null || !g.Started || GameState.P == null) return;
            var p = g.player;
            if (p != watched) { watched = p; lastHp = -1f; alertAt.Clear(); }

            // tempo (para ao concluir)
            bool any = false;
            if (!g.Traveling && dt > 0f)
                foreach (var a in G.active)
                    if (!a.done && a.v > 0) { a.time += dt; any = true; }

            // dano sofrido: a vida caiu (só na Torre, fora de viagens)
            float hp = GameState.P.hp;
            if (lastHp >= 0f && hp < lastHp - 0.01f && g.InDungeon && !g.Traveling && p != null)
            {
                float dmg = lastHp - hp;
                foreach (var a in G.active) if (!a.done && a.v > 0) a.dmg += dmg;
                Achievements.OnDamaged(dmg);
            }
            lastHp = hp;

            // alertar → abater
            if (g.InDungeon && !g.Traveling)
            {
                foreach (var e in g.enemies)
                    if (e != null && !e.dead && e.alerted && !alertAt.ContainsKey(e)) alertAt[e] = Time.time;
                if (alertAt.Count > 0)
                {
                    tmp.Clear();
                    foreach (var kv in alertAt) if (kv.Key == null || kv.Key.dead) tmp.Add(kv.Key);
                    foreach (var e in tmp)
                    {
                        float since;
                        if (!alertAt.TryGetValue(e, out since)) continue;
                        alertAt.Remove(e);
                        if (e == null) continue;   // destruído sem morrer (fim do evento, viagem)
                        float took = Mathf.Clamp(Time.time - since, 0f, 120f);
                        foreach (var a in G.active) if (!a.done && a.v > 0) { a.akSum += took; a.akN++; }
                    }
                    tmp.Clear();
                }
            }
            if (any) GuildState.MarkDirty();
        }
    }
}
