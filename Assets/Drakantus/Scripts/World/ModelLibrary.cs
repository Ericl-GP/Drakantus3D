using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Liga um nome lógico ("hero_knight", "tree", "wall"...) ao modelo 3D do projeto.
    /// O menu Drakantus > Montar biblioteca de modelos preenche isto sozinho procurando
    /// os modelos KayKit em Assets. Para trocar um modelo, abra
    /// Assets/Drakantus/Resources/ModelLibrary.asset e arraste outro prefab/FBX no campo.
    /// </summary>
    [CreateAssetMenu(menuName = "Drakantus/Model Library", fileName = "ModelLibrary")]
    public class ModelLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public GameObject prefab;
            public Vector3 rotation;
            public float scale = 1f;
        }

        public List<Entry> entries = new();
        public RuntimeAnimatorController characterAnimator;

        Dictionary<string, Entry> map;

        public Entry Get(string id)
        {
            if (map == null || map.Count != entries.Count)
            {
                map = new Dictionary<string, Entry>();
                foreach (var e in entries) if (e != null && !string.IsNullOrEmpty(e.id) && e.prefab != null) map[e.id] = e;
            }
            return map.TryGetValue(id, out var r) ? r : null;
        }

        public void Set(string id, GameObject prefab)
        {
            var e = entries.Find(x => x.id == id);
            if (e == null) { e = new Entry { id = id }; entries.Add(e); }
            e.prefab = prefab;
            map = null;
        }

        static ModelLibrary inst;
        public static ModelLibrary I
        {
            get
            {
                if (inst == null) inst = Resources.Load<ModelLibrary>("ModelLibrary");
                if (inst == null) inst = CreateInstance<ModelLibrary>();
                return inst;
            }
        }
    }

    /// <summary>Cria modelos pelo nome lógico; se não houver modelo, monta uma forma simples no lugar.</summary>
    public static class Models
    {
        public static bool Has(string id) => ModelLibrary.I.Get(id) != null;

        /// <summary>Instancia o modelo. Retorna null se não existir (use Fallback).</summary>
        public static GameObject Spawn(string id, Transform parent, Vector3 pos, float yaw = 0f, float scale = 1f)
        {
            var e = ModelLibrary.I.Get(id);
            if (e == null) return null;
            var g = UnityEngine.Object.Instantiate(e.prefab, parent);
            g.name = id;
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(e.rotation);
            g.transform.localScale = Vector3.one * scale * (e.scale <= 0 ? 1f : e.scale);
            return g;
        }

        /// <summary>Instancia o modelo ou, se faltar, a forma simples definida aqui.</summary>
        public static GameObject SpawnOr(string id, Transform parent, Vector3 pos, float yaw = 0f, float scale = 1f)
        {
            var g = Spawn(id, parent, pos, yaw, scale);
            if (g != null) return g;
            return Fallback(id, parent, pos, yaw, scale);
        }

        /// <summary>Formas simples para cada tipo de objeto (o jogo roda mesmo sem modelos).</summary>
        public static GameObject Fallback(string id, Transform parent, Vector3 pos, float yaw = 0f, float scale = 1f)
        {
            var g = new GameObject(id + "_simples");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            g.transform.localScale = Vector3.one * scale;
            var t = g.transform;
            string k = id.ToLowerInvariant();
            if (k.StartsWith("tree"))
            {
                U.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.8f, 0), new Vector3(0.35f, 0.8f, 0.35f), U.Hex("6b4a32"));
                U.Prim(PrimitiveType.Sphere, t, new Vector3(0, 2.3f, 0), new Vector3(2.2f, 2.0f, 2.2f), U.Hex(k.Contains("pine") ? "2f6b44" : "4f9a4a"));
            }
            else if (k.StartsWith("bush")) U.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0.4f, 0), new Vector3(1.2f, 0.8f, 1.2f), U.Hex("5aa851"));
            else if (k.StartsWith("rock")) U.Prim(PrimitiveType.Sphere, t, new Vector3(0, 0.35f, 0), new Vector3(1.2f, 0.7f, 1f), U.Hex("8a8a90"));
            else if (k.StartsWith("grass")) U.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.1f, 0), new Vector3(0.4f, 0.2f, 0.4f), U.Hex("6dbb5a"));
            else if (k.StartsWith("house") || k.StartsWith("building"))
            {
                U.Prim(PrimitiveType.Cube, t, new Vector3(0, 1.5f, 0), new Vector3(4, 3, 4), U.Hex("e8d6b0"));
                var roof = U.Prim(PrimitiveType.Cube, t, new Vector3(0, 3.6f, 0), new Vector3(3.2f, 3.2f, 4.4f), U.Hex(k.Contains("blue") ? "4a6ab0" : "b0503a"));
                roof.transform.localRotation = Quaternion.Euler(0, 0, 45);
                U.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.8f, -2.01f), new Vector3(1f, 1.6f, 0.1f), U.Hex("6b4a32"));
            }
            else if (k.StartsWith("tower"))
            {
                U.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 5f, 0), new Vector3(5, 5, 5), U.Hex("8a8090"));
                U.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 10.5f, 0), new Vector3(5.6f, 0.5f, 5.6f), U.Hex("5a5060"));
                U.Prim(PrimitiveType.Cube, t, new Vector3(0, 1.4f, -2.45f), new Vector3(1.6f, 2.8f, 0.2f), U.Hex("3a2a4a"));
            }
            else if (k.StartsWith("wall")) U.Prim(PrimitiveType.Cube, t, new Vector3(0, 1.5f, 0), new Vector3(4, 3, 0.5f), U.Hex("7a7480"));
            else if (k.StartsWith("pillar")) U.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 1.6f, 0), new Vector3(0.7f, 1.6f, 0.7f), U.Hex("8a8490"));
            else if (k.StartsWith("floor")) U.Prim(PrimitiveType.Cube, t, new Vector3(0, -0.05f, 0), new Vector3(4, 0.1f, 4), U.Hex("6a6470"));
            else if (k.StartsWith("barrel")) U.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.5f, 0), new Vector3(0.8f, 0.5f, 0.8f), U.Hex("8a5a32"));
            else if (k.StartsWith("crate") || k.StartsWith("box")) U.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.45f, 0), Vector3.one * 0.9f, U.Hex("a0703a"));
            else if (k.StartsWith("chest"))
            {
                U.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.35f, 0), new Vector3(1f, 0.7f, 0.7f), U.Hex("8a5020"));
                U.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.75f, 0), new Vector3(1.05f, 0.15f, 0.75f), U.Hex("e0b040"));
            }
            else if (k.StartsWith("torch") || k.StartsWith("lamp"))
            {
                U.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 1f, 0), new Vector3(0.15f, 1f, 0.15f), U.Hex("4a3a2a"));
                U.Prim(PrimitiveType.Sphere, t, new Vector3(0, 2.1f, 0), Vector3.one * 0.3f, U.Hex("ffb347"));
            }
            else if (k.StartsWith("table")) U.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.45f, 0), new Vector3(2f, 0.9f, 1f), U.Hex("8a5a32"));
            else if (k.StartsWith("banner")) U.Prim(PrimitiveType.Cube, t, new Vector3(0, 2f, 0), new Vector3(0.9f, 1.8f, 0.05f), U.Hex("7a3ab0"));
            else if (k.StartsWith("fence")) U.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 0.15f), U.Hex("8a6a42"));
            else if (k.StartsWith("well")) U.Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.5f, 0), new Vector3(1.6f, 0.5f, 1.6f), U.Hex("9a9aa0"));
            else if (k.StartsWith("stall") || k.StartsWith("market")) U.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.6f, 0), new Vector3(2.4f, 1.2f, 1.2f), U.Hex("c0763a"));
            else U.Prim(PrimitiveType.Cube, t, new Vector3(0, 0.5f, 0), Vector3.one, U.Hex("c0c0c0"));
            return g;
        }
    }
}
