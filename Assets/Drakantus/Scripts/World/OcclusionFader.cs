using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Drakantus
{

    /// <summary>
    /// Deixa semitransparentes (25%) os Occluders entre a câmera e o herói.
    /// A cada 0,1 s: SphereCastAll (raio 0,6) da câmera até o herói e até 1 m acima dele, subindo de cada
    /// colisor atingido até o Occluder; e, como muitas árvores não têm colisor próprio, também testa
    /// o raio contra a caixa de cada Occluder registrado. O fade troca os materiais opacos (URP/Lit)
    /// por cópias transparentes (cache por material) e controla o alpha com MaterialPropertyBlock;
    /// ao sair, restaura os materiais originais. Adicionado pela CameraRig.
    /// </summary>
    public class OcclusionFader : MonoBehaviour
    {
        public float interval = 0.1f;
        public float radius = 0.6f;
        public float fadedAlpha = 0.25f;
        public float fadeSpeed = 3.5f;   // alpha por segundo

        class Fade
        {
            public Occluder occ;
            public Renderer[] rends;
            public Material[][] originals;
            public float alpha = 1f, target = 1f;
            public bool swapped;
        }

        readonly Dictionary<Occluder, Fade> fades = new Dictionary<Occluder, Fade>();
        readonly Dictionary<Material, Material> transparentCache = new Dictionary<Material, Material>();
        readonly HashSet<Occluder> hitNow = new HashSet<Occluder>();
        readonly List<Occluder> toRemove = new List<Occluder>();
        readonly RaycastHit[] buf = new RaycastHit[48];
        MaterialPropertyBlock mpb;
        float clock;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake() { mpb = new MaterialPropertyBlock(); }

        void LateUpdate()
        {
            float udt = Time.unscaledDeltaTime;
            clock -= udt;
            if (clock <= 0f)
            {
                clock = interval;
                Detect();
            }
            Animate(udt);
        }

        // ------------------------------------------------------------------ detecção
        void Detect()
        {
            hitNow.Clear();
            var rig = CameraRig.I;
            Transform tgt = rig != null ? rig.target : null;
            if (tgt != null)
            {
                Vector3 cam = transform.position;
                Vector3 hero = tgt.position;
                Probe(cam, hero + Vector3.up * 0.9f);
                Probe(cam, hero + Vector3.up * 1.9f);
            }

            foreach (var o in hitNow)
            {
                if (!fades.TryGetValue(o, out var f))
                {
                    f = new Fade { occ = o };
                    fades[o] = f;
                }
                f.target = fadedAlpha;
            }
            foreach (var kv in fades)
                if (!hitNow.Contains(kv.Key)) kv.Value.target = 1f;
        }

        void Probe(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return;
            Vector3 dir = d / len;

            // 1) física: colisores dos objetos grandes (casas, rochas, muros com colisor próprio)
            int n = Physics.SphereCastNonAlloc(from, radius, dir, buf, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = buf[i].collider;
                if (c == null || buf[i].distance <= 0f) continue;   // 0 = esfera já nasceu encostada (parede ao lado da câmera)
                var o = c.GetComponentInParent<Occluder>();
                if (o != null && o.isActiveAndEnabled) hitNow.Add(o);
            }

            // 2) caixas: árvores e blocos sem colisor (raio do herói em direção à câmera)
            var ray = new Ray(to, -dir);
            var all = Occluder.All;
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o == null || hitNow.Contains(o)) continue;
                if (!o.TryGetBox(out var b)) continue;
                b.Expand(radius * 0.5f);
                if (b.IntersectRay(ray, out float dist) && dist < len - 0.3f) hitNow.Add(o);
            }
        }

        // ------------------------------------------------------------------ animação do fade
        void Animate(float udt)
        {
            toRemove.Clear();
            foreach (var kv in fades)
            {
                var f = kv.Value;
                if (kv.Key == null) { toRemove.Add(kv.Key); continue; }   // mapa destruído
                f.alpha = Mathf.MoveTowards(f.alpha, f.target, fadeSpeed * udt);

                if (f.alpha < 0.999f)
                {
                    if (!f.swapped) Swap(f);
                    SetAlpha(f, f.alpha);
                }
                else if (f.target >= 1f)
                {
                    if (f.swapped) Restore(f);
                    toRemove.Add(kv.Key);
                }
            }
            for (int i = 0; i < toRemove.Count; i++) fades.Remove(toRemove[i]);
        }

        void Swap(Fade f)
        {
            f.rends = f.occ.Renderers;
            f.originals = new Material[f.rends.Length][];
            for (int i = 0; i < f.rends.Length; i++)
            {
                var r = f.rends[i];
                if (r == null) continue;
                var orig = r.sharedMaterials;
                f.originals[i] = orig;
                var copy = new Material[orig.Length];
                for (int k = 0; k < orig.Length; k++) copy[k] = Transparent(orig[k]);
                r.sharedMaterials = copy;
            }
            f.swapped = true;
        }

        void Restore(Fade f)
        {
            if (f.rends == null) { f.swapped = false; return; }
            for (int i = 0; i < f.rends.Length; i++)
            {
                var r = f.rends[i];
                if (r == null || f.originals[i] == null) continue;
                r.sharedMaterials = f.originals[i];
                r.SetPropertyBlock(null);
                for (int k = 0; k < f.originals[i].Length; k++) r.SetPropertyBlock(null, k);
            }
            f.swapped = false;
        }

        void SetAlpha(Fade f, float a)
        {
            if (f.rends == null) return;
            for (int i = 0; i < f.rends.Length; i++)
            {
                var r = f.rends[i];
                if (r == null || f.originals[i] == null) continue;
                var orig = f.originals[i];
                for (int k = 0; k < orig.Length; k++)
                {
                    var m = orig[k];
                    if (m == null) continue;
                    Color c = Color.white;
                    int id = -1;
                    if (m.HasProperty(BaseColorId)) { c = m.GetColor(BaseColorId); id = BaseColorId; }
                    else if (m.HasProperty(ColorId)) { c = m.GetColor(ColorId); id = ColorId; }
                    if (id < 0) continue;
                    c.a = a * c.a;
                    mpb.Clear();
                    r.GetPropertyBlock(mpb, k);
                    mpb.SetColor(id, c);
                    r.SetPropertyBlock(mpb, k);
                }
            }
        }

        /// <summary>Cópia transparente (alpha blend) de um material URP/Lit opaco; uma por material original.</summary>
        Material Transparent(Material orig)
        {
            if (orig == null) return null;
            if (transparentCache.TryGetValue(orig, out var cached) && cached != null) return cached;
            var m = new Material(orig);
            m.name = orig.name + " (Fade)";
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;   // 3000
            transparentCache[orig] = m;
            return m;
        }

        void OnDestroy()
        {
            foreach (var kv in fades)
                if (kv.Key != null && kv.Value.swapped) Restore(kv.Value);
            fades.Clear();
            foreach (var kv in transparentCache)
                if (kv.Value != null) Destroy(kv.Value);
            transparentCache.Clear();
        }
    }
}
