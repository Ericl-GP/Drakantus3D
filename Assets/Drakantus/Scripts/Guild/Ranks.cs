using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Rank da Guilda: F, E, D, C, B, A, S, SS, SSS, DEUS (Resources/Data/ranks.json).
    /// Pontos de Guilda (PG) vêm das missões. Ao atingir os pontos do próximo rank aparece a
    /// Prova de Promoção no quadro; ao entregá-la o herói sobe de rank (cerimônia + recompensas).
    /// </summary>
    public static class Ranks
    {
        static GuildProfile G => GuildState.G;

        public static int Count { get { GuildData.Load(); return GuildData.Ranks.Count; } }
        public static int Current => G.rank;
        public static bool IsMax => G.rank >= Count - 1;
        public static RankDef Def(int i) => GuildData.Rank(i);
        public static RankDef CurrentDef => Def(G.rank);
        public static RankDef NextDef => IsMax ? null : Def(G.rank + 1);
        public static string Letter(int i) => Def(i).name ?? "?";

        /// <summary>Tem pontos para a próxima promoção (falta só a prova).</summary>
        public static bool TrialReady => !IsMax && G.points >= Def(G.rank + 1).points;

        /// <summary>Progresso de pontos entre o rank atual e o próximo (0..1).</summary>
        public static float Progress(out int have, out int need)
        {
            have = G.points;
            if (IsMax) { need = G.points; return 1f; }
            int from = CurrentDef.points, to = NextDef.points;
            need = to;
            return Mathf.Clamp01((G.points - from) / (float)Mathf.Max(1, to - from));
        }

        /// <summary>Cor base do rank.</summary>
        public static Color BaseColor(int i) => U.Hex(Def(i).color);

        /// <summary>Cor animada: SSS = arco-íris, DEUS = dourado/branco pulsante, outros = cor fixa.</summary>
        public static Color AnimColor(int i, float t)
        {
            string id = Def(i).id ?? "";
            if (id == "SSS") return Color.HSVToRGB(Mathf.Repeat(t * 0.25f, 1f), 0.75f, 1f);
            if (id == "DEUS")
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 4f);
                return Color.Lerp(U.Hex("ffc83a"), Color.white, k);
            }
            return BaseColor(i);
        }

        /// <summary>Avisa (uma vez por rank) que a Prova de Promoção está liberada.</summary>
        public static void CheckPromotion()
        {
            if (!TrialReady) { G.trialAnnounced = false; return; }
            if (G.trialAnnounced) return;
            G.trialAnnounced = true;
            GameState.Notify("Prova de Promoção para o Rank " + NextDef.name + " disponível no quadro da Guilda!");
            Sfx.Play("item_rare");
            GuildState.MarkDirty();
        }

        /// <summary>Sobe um rank: recompensas, título e cerimônia.</summary>
        public static void Promote()
        {
            if (IsMax) return;
            G.rank++;
            G.trialAnnounced = false;
            var d = CurrentDef;
            if (!string.IsNullOrEmpty(d.title)) G.title = d.title;
            if (d.coins > 0) GameState.AddCoins(d.coins);
            if (d.items != null)
                foreach (var it in d.items)
                {
                    var idf = GameData.Item(it);
                    if (idf == null) continue;
                    GameState.Grant(it);
                    GameState.Notify("Recompensa de rank: " + idf.name);
                }
            // remove provas antigas que ficaram para trás
            G.active.RemoveAll(a => a.id.StartsWith(Quests.PromoPrefix) && a.id != Quests.PromoPrefix + (G.rank + 1));
            GameState.Save();
            GuildState.Save();
            GuildState.Emit();

            var pl = Game.I != null ? Game.I.player : null;
            if (pl != null)
            {
                Vector3 p = pl.transform.position;
                FX.Pillar(p, U.Hex("ffd76a"), 2f);
                FX.Ring(p, 4f, BaseColor(G.rank), 0.8f);
                FX.Burst(p + Vector3.up * 1.5f, Color.white, 1.4f, 50);
                if (Game.I != null) Game.I.Shake(0.25f);
            }
            Sfx.Play("levelup");
            if (HUD.I != null) HUD.I.RankCeremony(G.rank);
            CheckPromotion();
            Lore.CheckUnlocks(true);     // [Progressão] páginas "rank:"
            Achievements.Check();
        }

        /// <summary>Texto curto das recompensas de um rank.</summary>
        public static string RewardText(int i)
        {
            var d = Def(i);
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(d.title)) parts.Add("Título \"" + d.title + "\"");
            if (d.coins > 0) parts.Add(d.coins + " moedas");
            if (d.items != null)
                foreach (var it in d.items)
                {
                    var idf = GameData.Item(it);
                    if (idf == null) continue;
                    parts.Add("<color=#" + ColorUtility.ToHtmlStringRGB(GameData.RarityColor(idf)) + ">" + idf.name + "</color>");
                }
            return parts.Count > 0 ? string.Join("  ·  ", parts) : "—";
        }
    }
}
