using System.Collections;
using UnityEngine;

namespace Drakantus
{
    /// <summary>Recebe sinais de placas, alavancas e gatilhos de sala (via TriggerLink).</summary>
    public abstract class FloorReceiver : MonoBehaviour
    {
        /// <summary>on = true: abrir/ativar/disparar. on = false: fechar/desativar.</summary>
        public abstract void Signal(bool on);
    }
}
