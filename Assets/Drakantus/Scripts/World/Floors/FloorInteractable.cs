using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Interactable com ação própria (alavancas, paredes secretas, portas trancadas).</summary>
    public class FloorInteractable : Interactable
    {
        public System.Action onUse;

        public override void Interact()
        {
            if (onUse != null) onUse();
        }

        /// <summary>Cria e registra no Game (tecla E + dica na tela ficam por conta do Game).</summary>
        public static FloorInteractable Add(GameObject go, string label, float radius, System.Action onUse)
        {
            var it = go.AddComponent<FloorInteractable>();
            it.kind = "floor";
            it.label = label;
            it.radius = radius;
            it.onUse = onUse;
            if (Game.I != null && !Game.I.interactables.Contains(it)) Game.I.interactables.Add(it);
            return it;
        }
    }
}
