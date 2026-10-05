using UnityEditor;
using UnityEditor.Build;

namespace Drakantus.EditorTools
{
    /// <summary>
    /// Adiciona um símbolo de compilação uma única vez. Isso muda a chave do cache de compilação
    /// e obriga a Unity a recompilar também os pacotes (URP, Shader Graph, Input System),
    /// descartando DLLs geradas enquanto o Windows bloqueava o gerador de código da Unity.
    /// </summary>
    [InitializeOnLoad]
    static class ForceRebuild
    {
        const string Symbol = "DRAKANTUS_REBUILD_1";

        static ForceRebuild()
        {
            var target = NamedBuildTarget.Standalone;
            string defs = PlayerSettings.GetScriptingDefineSymbols(target);
            if (defs.Contains(Symbol)) return;
            PlayerSettings.SetScriptingDefineSymbols(target, string.IsNullOrEmpty(defs) ? Symbol : defs + ";" + Symbol);
            UnityEngine.Debug.Log("[Drakantus] Recompilando todos os pacotes (" + Symbol + ").");
        }
    }
}
