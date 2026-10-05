using UnityEngine;
using UnityEngine.SceneManagement;

namespace Drakantus
{
    /// <summary>
    /// Inicia o jogo sozinho: ao carregar a cena "Drakantus" (ou qualquer cena com um
    /// objeto chamado "DrakantusBoot"), cria o objeto Game se ainda não existir.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Game.I != null) return;
            var scene = SceneManager.GetActiveScene();
            bool ok = scene.name == "Drakantus" || GameObject.Find("DrakantusBoot") != null;
            if (!ok) return;
            var go = new GameObject("Game");
            go.AddComponent<Game>();
        }
    }
}
