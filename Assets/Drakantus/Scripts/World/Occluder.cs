using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Marcador (sem lógica de jogo) para objetos GRANDES que podem tampar o herói: árvores, casas,
    /// muralhas, rochas grandes, paredes da masmorra. O LevelBuilder adiciona no objeto raiz do modelo.
    /// Guarda os Renderers e a caixa (bounds) para o OcclusionFader.
    /// </summary>
    public class Occluder : MonoBehaviour
    {
        public static readonly List<Occluder> All = new List<Occluder>();

        Renderer[] rends;
        Bounds box;
        bool measured, hasBox;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Renderers de malha do objeto (partículas ficam de fora).</summary>
        public Renderer[] Renderers
        {
            get
            {
                if (rends == null)
                {
                    var list = new List<Renderer>();
                    foreach (var r in GetComponentsInChildren<Renderer>(true))
                        if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r);
                    rends = list.ToArray();
                }
                return rends;
            }
        }

        /// <summary>Caixa no mundo, encolhida um pouco no plano (copas de árvore são "redondas").</summary>
        public bool TryGetBox(out Bounds b)
        {
            if (!measured)
            {
                measured = true;
                hasBox = false;
                foreach (var r in Renderers)
                {
                    if (r == null) continue;
                    if (!hasBox) { box = r.bounds; hasBox = true; }
                    else box.Encapsulate(r.bounds);
                }
                if (hasBox)
                {
                    Vector3 e = box.extents;
                    box.extents = new Vector3(e.x * 0.82f, e.y, e.z * 0.82f);
                }
            }
            b = box;
            return hasBox;
        }

        /// <summary>Força medir de novo (se o objeto for movido/escalado depois).</summary>
        public void Remeasure() { measured = false; rends = null; }
    }
}
