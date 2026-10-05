using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Heróis do grupo. Hoje só o jogador local (registrado no OnEnable do Player);
    /// no multiplayer os jogadores da rede entram aqui também.
    /// Habilidades de suporte (Sacerdote, The Guard) sempre usam <see cref="InRadius"/>.
    /// </summary>
    public static class Party
    {
        public static readonly List<Player> Members = new();

        public static void Register(Player p)
        {
            if (p != null && !Members.Contains(p)) Members.Add(p);
        }

        public static void Unregister(Player p)
        {
            Members.Remove(p);
        }

        /// <summary>Membros vivos (ou caídos, para ressurreição: use <see cref="AllInRadius"/>) dentro do raio (plano XZ).</summary>
        public static List<Player> InRadius(Vector3 center, float r)
        {
            var l = new List<Player>();
            for (int i = Members.Count - 1; i >= 0; i--)
            {
                var p = Members[i];
                if (p == null) { Members.RemoveAt(i); continue; }
                if (p.state == "dead") continue;
                if (U.Flat(p.transform.position - center).magnitude <= r) l.Add(p);
            }
            return l;
        }

        /// <summary>Como InRadius, mas inclui os caídos.</summary>
        public static List<Player> AllInRadius(Vector3 center, float r)
        {
            var l = new List<Player>();
            for (int i = Members.Count - 1; i >= 0; i--)
            {
                var p = Members[i];
                if (p == null) { Members.RemoveAt(i); continue; }
                if (U.Flat(p.transform.position - center).magnitude <= r) l.Add(p);
            }
            return l;
        }
    }
}
