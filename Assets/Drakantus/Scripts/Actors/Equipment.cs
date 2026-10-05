using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Equipamento visível no herói (e no boneco de prévia da bolsa):
    /// - capacete: malha "doadora" de outro personagem KayKit (ex.: Knight_Helmet, Mage_Hat, Barbarian_BearHat)
    ///   religada aos ossos do herói (todos usam o mesmo Rig_Medium); sem doador -> elmo simples de primitivas.
    /// - capa: malha plana curvada gerada em código, presa no osso "chest", balançando (CapeSway).
    /// - asas: duas malhas em forma de asa geradas em código, presas nas costas, batendo (WingFlap), brilho por raridade.
    /// - peitoral / calça / bota: tinta (MaterialPropertyBlock) nas partes Body+Arms / Legs do modelo.
    /// - aparência (cabelo e roupa): textura atlas recolorida em runtime (AppearanceTex) aplicada como _BaseMap.
    /// Chamado por Player.RefreshEquipment e pelo PreviewRig (Inventory.cs).
    /// </summary>
    public static class Equipment
    {
        // ------------------------------------------------------------------ arma (mesma regra do Player)
        public static void WeaponFor(out string weapon, out string offhand, out string glow)
        {
            var cd = GameState.Class;
            var w = GameState.Equipped("arma");
            // só armas que a classe pode usar aparecem na mão; sem arma -> arma padrão da classe
            bool usable = w != null && !string.IsNullOrEmpty(w.model) && GameData.ClassCanUse(cd, w);
            string wm = usable ? w.model : cd.weapon;
            if (cd.id == "sacerdote" && wm == "staff") wm = "staff_holy";        // cajado sagrado (modelo do Blender)
            if (w != null && (w.id == "blood_axes" || w.id == "ragnarok_axes")) wm = "axe_blood";
            weapon = wm;
            // classes de duas armas iguais (adagas, machados, pistolas): a mão esquerda copia a arma equipada
            offhand = !string.IsNullOrEmpty(cd.offhand) && cd.offhand == cd.weapon ? wm : cd.offhand;
            glow = w != null ? GameData.RarityGlow(w) : "";
        }

        public static Color ItemTint(ItemDef d)
        {
            if (d == null || string.IsNullOrEmpty(d.tint)) return Color.white;
            return U.Hex(d.tint);
        }

        static bool Ends(string n, string suffix) => n != null && n.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase);

        static bool IsHeadwear(string n) => Ends(n, "_Helmet") || Ends(n, "_HelmetVisor") || Ends(n, "_Hat") || Ends(n, "_BearHat");

        // ------------------------------------------------------------------ aplicar tudo
        /// <summary>Remonta o equipamento visível + aparência do herói a partir do GameState.</summary>
        public static void Apply(CharacterVisual v)
        {
            if (v == null || v.model == null) return;
            v.ClearEquipmentVisual();

            var helm = GameState.Equipped("capacete");
            var chest = GameState.Equipped("peitoral");
            var pants = GameState.Equipped("calca");
            var boots = GameState.Equipped("bota");
            var cape = GameState.Equipped("capa");
            var wings = GameState.Equipped("asa");

            // cópia da lista (vamos adicionar peças depois)
            var body = new List<Renderer>(v.BodyRenderers);

            // ---- tintas por parte + esconder chapéus/capas originais
            if (!v.simple)
            {
                Color chestC = ItemTint(chest);
                Color legC = Color.white;
                if (pants != null) legC = ItemTint(pants);
                if (boots != null) legC *= Color.Lerp(Color.white, ItemTint(boots), 0.45f);   // pernas e botas são a mesma malha: mistura sutil
                foreach (var r in body)
                {
                    if (r == null) continue;
                    string n = r.name;
                    if (chest != null && (Ends(n, "_Body") || Ends(n, "_ArmLeft") || Ends(n, "_ArmRight"))) v.partTint[r] = chestC;
                    else if ((pants != null || boots != null) && (Ends(n, "_LegLeft") || Ends(n, "_LegRight"))) v.partTint[r] = legC;
                    if ((helm != null && IsHeadwear(n)) || (cape != null && Ends(n, "_Cape")))
                    {
                        if (r.enabled) { r.enabled = false; v.hiddenParts.Add(r); }
                    }
                }
            }
            else if (chest != null)
            {
                Color c = Color.Lerp(Color.white, ItemTint(chest), 0.6f);
                foreach (var r in body) if (r != null) v.partTint[r] = c;
            }

            // ---- aparência (cabelo / roupa)
            if (!v.simple)
            {
                Color? hc = AppearanceTex.Resolve(GameState.P.hairColor, GameState.P.hairTone);
                Color? c1 = AppearanceTex.Resolve(GameState.P.clothColor1, GameState.P.cloth1Tone);
                Color? c2 = AppearanceTex.Resolve(GameState.P.clothColor2, GameState.P.cloth2Tone);
                if (hc.HasValue || c1.HasValue || c2.HasValue)
                {
                    foreach (var r in body)
                    {
                        if (r == null) continue;
                        var t = AppearanceTex.For(r, hc, c1, c2);
                        if (t != null) v.partMap[r] = t;
                    }
                }
            }

            // ---- peças 3D
            var measure = Measure(v);
            if (helm != null)
            {
                GameObject g = null;
                if (!v.simple && !string.IsNullOrEmpty(helm.model)) g = AttachDonor(v, helm.model, ItemTint(helm));
                if (g == null) g = BuildHelmet(v, helm, measure);
                if (g != null) v.equipObjects.Add(g);
            }
            if (cape != null)
            {
                var g = BuildCape(v, cape, measure);
                if (g != null) v.equipObjects.Add(g);
            }
            if (wings != null)
            {
                var g = BuildWings(v, wings, measure);
                if (g != null) v.equipObjects.Add(g);
            }

            v.RefreshColors();
        }

        // ------------------------------------------------------------------ medidas do corpo (espaço local do CharacterVisual, pose de ligação)
        struct BodyInfo { public Bounds torso, head; public bool hasTorso, hasHead; public float height; }

        static BodyInfo Measure(CharacterVisual v)
        {
            var bi = new BodyInfo { height = Mathf.Max(1f, v.height) };
            foreach (var r in v.BodyRenderers)
            {
                if (r == null) continue;
                if (!bi.hasTorso && Ends(r.name, "_Body")) bi.hasTorso = LocalMeshBounds(v.transform, r, out bi.torso);
                else if (!bi.hasHead && Ends(r.name, "_Head")) bi.hasHead = LocalMeshBounds(v.transform, r, out bi.head);
            }
            float h = bi.height;
            if (bi.hasTorso && (bi.torso.size.y < 0.1f || bi.torso.size.y > h || bi.torso.center.y < 0f || bi.torso.center.y > h)) bi.hasTorso = false;
            if (bi.hasHead && (bi.head.size.y < 0.1f || bi.head.size.y > h || bi.head.center.y < h * 0.3f || bi.head.center.y > h * 1.2f)) bi.hasHead = false;
            if (!bi.hasTorso) bi.torso = new Bounds(new Vector3(0f, h * 0.5f, 0f), new Vector3(h * 0.36f, h * 0.3f, h * 0.24f));
            if (!bi.hasHead) bi.head = new Bounds(new Vector3(0f, h * 0.8f, 0f), new Vector3(h * 0.36f, h * 0.32f, h * 0.34f));
            return bi;
        }

        /// <summary>Caixa da malha (pose de ligação) no espaço local de "space" — não depende da animação nem da rotação.</summary>
        static bool LocalMeshBounds(Transform space, Renderer r, out Bounds b)
        {
            b = default;
            Mesh m = null;
            if (r is SkinnedMeshRenderer smr) m = smr.sharedMesh;
            else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) m = mf.sharedMesh; }
            if (m == null) return false;
            var mb = m.bounds;
            bool first = true;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = space.InverseTransformPoint(r.transform.TransformPoint(c));
                if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                else b.Encapsulate(p);
            }
            return true;
        }

        /// <summary>Cria um pivô preso ao osso, com pose = (posição local do personagem, rotação do personagem), escala 1 no mundo.</summary>
        static Transform Pivot(CharacterVisual v, string name, Transform bone, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(bone != null ? bone : v.transform, false);
            t.position = v.transform.TransformPoint(localPos);
            t.rotation = v.transform.rotation;
            var ps = t.parent.lossyScale;
            float k = v.transform.parent != null ? v.transform.parent.lossyScale.x : 1f;
            t.localScale = new Vector3(k / Mathf.Max(0.0001f, Mathf.Abs(ps.x)), k / Mathf.Max(0.0001f, Mathf.Abs(ps.y)), k / Mathf.Max(0.0001f, Mathf.Abs(ps.z)));
            return t;
        }

        static Renderer Piece(CharacterVisual v, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c, Vector3? euler = null)
        {
            var g = U.Prim(type, parent, pos, scale, Color.white);
            if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
            var r = g.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            v.AddPartRenderer(r, c);
            return r;
        }

        static Color TrimColor(ItemDef d)
        {
            if (d == null) return U.Hex("c9a45a");
            if (d.rarity == "lendario") return U.Hex("ffc640");
            if (d.rarity == "raro") return U.Hex("8fc4ff");
            return U.Hex("a89a88");
        }

        // ------------------------------------------------------------------ capacete
        static Transform donorRoot;
        static readonly Dictionary<string, GameObject> donorModels = new();

        static Renderer DonorRenderer(string meshName)
        {
            int us = meshName.IndexOf('_');
            if (us <= 0) return null;
            string prefix = meshName.Substring(0, us);
            string modelId = "hero_" + (prefix == "RogueHooded" ? "Rogue_Hooded" : prefix);
            if (!donorModels.TryGetValue(modelId, out var g) || g == null)
            {
                if (donorRoot == null)
                {
                    var root = new GameObject("DoadoresDeEquipamento");
                    root.SetActive(false);
                    Object.DontDestroyOnLoad(root);
                    donorRoot = root.transform;
                }
                g = Models.Spawn(modelId, donorRoot, Vector3.zero, 0f, 1f);
                donorModels[modelId] = g;
            }
            if (g == null) return null;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                if (string.Equals(r.name, meshName, System.StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        /// <summary>Copia a malha de outro personagem KayKit e religa aos ossos (mesmo nome) do herói.</summary>
        static GameObject AttachDonor(CharacterVisual v, string meshName, Color tint)
        {
            var src = DonorRenderer(meshName);
            if (src == null) return null;
            var map = new Dictionary<string, Transform>();
            foreach (var t in v.model.GetComponentsInChildren<Transform>(true))
                if (!map.ContainsKey(t.name)) map[t.name] = t;

            if (src is SkinnedMeshRenderer ss)
            {
                var sb = ss.bones;
                var nb = new Transform[sb.Length];
                for (int i = 0; i < sb.Length; i++)
                {
                    if (sb[i] == null) continue;
                    if (!map.TryGetValue(sb[i].name, out nb[i])) return null;
                }
                var g = new GameObject("equip_" + meshName);
                g.transform.SetParent(v.model, false);
                var smr = g.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = ss.sharedMesh;
                smr.sharedMaterials = ss.sharedMaterials;
                smr.bones = nb;
                Transform rb = null;
                if (ss.rootBone != null) map.TryGetValue(ss.rootBone.name, out rb);
                smr.rootBone = rb;
                smr.localBounds = ss.localBounds;
                smr.quality = ss.quality;
                smr.updateWhenOffscreen = true;
                v.AddPartRenderer(smr, tint);
                return g;
            }
            var mf = src.GetComponent<MeshFilter>();
            if (mf != null && src.transform.parent != null && map.TryGetValue(src.transform.parent.name, out var bone))
            {
                var g = new GameObject("equip_" + meshName);
                g.transform.SetParent(bone, false);
                g.transform.localPosition = src.transform.localPosition;
                g.transform.localRotation = src.transform.localRotation;
                g.transform.localScale = src.transform.localScale;
                g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterials = src.sharedMaterials;
                v.AddPartRenderer(mr, tint);
                return g;
            }
            return null;
        }

        /// <summary>Elmo simples (quando não há modelo doador): cúpula + aba + crista no lendário.</summary>
        static GameObject BuildHelmet(CharacterVisual v, ItemDef d, BodyInfo bi)
        {
            var hb = bi.head;
            float s = Mathf.Max(hb.size.x, hb.size.z);
            var root = Pivot(v, "elmo", v.headBone, hb.center + Vector3.up * hb.size.y * 0.22f);
            Color c = string.IsNullOrEmpty(d.tint) ? U.Hex("b8c0c8") : ItemTint(d);
            Color trim = TrimColor(d);
            Piece(v, PrimitiveType.Sphere, root, Vector3.zero, new Vector3(s * 1.08f, hb.size.y * 0.8f, s * 1.08f), c);
            Piece(v, PrimitiveType.Cylinder, root, new Vector3(0f, -hb.size.y * 0.12f, 0f), new Vector3(s * 1.16f, hb.size.y * 0.035f, s * 1.16f), trim);
            Piece(v, PrimitiveType.Cube, root, new Vector3(0f, -hb.size.y * 0.18f, s * 0.53f), new Vector3(s * 0.09f, hb.size.y * 0.3f, s * 0.06f), trim);
            if (d.rarity == "lendario" || d.rarity == "raro")
                Piece(v, PrimitiveType.Cube, root, new Vector3(0f, hb.size.y * 0.42f, -s * 0.05f), new Vector3(s * 0.08f, hb.size.y * 0.28f, s * 0.75f), trim);
            return root.gameObject;
        }

        // ------------------------------------------------------------------ capa
        static GameObject BuildCape(CharacterVisual v, ItemDef d, BodyInfo bi)
        {
            var tb = bi.torso;
            float width = Mathf.Clamp(tb.size.x * 0.95f, 0.25f, bi.height * 0.5f);
            Vector3 top = new Vector3(0f, tb.max.y - tb.size.y * 0.06f, tb.min.z - 0.03f);
            float len = Mathf.Max(0.4f, top.y * 0.8f);
            var pivot = Pivot(v, "capa", v.chestBone != null ? v.chestBone : v.spineBone, top);
            var cloth = new GameObject("tecido");
            cloth.transform.SetParent(pivot, false);
            cloth.AddComponent<MeshFilter>().sharedMesh = CapeMesh(width, len);
            var mr = cloth.AddComponent<MeshRenderer>();
            mr.sharedMaterial = U.Lit(Color.white, 0.12f);
            Color c = string.IsNullOrEmpty(d.tint) ? U.Hex("8a3a3a") : ItemTint(d);
            v.AddPartRenderer(mr, c);
            // barra dourada nas raras/lendárias
            if (d.rarity != "comum")
            {
                var hem = new GameObject("barra");
                hem.transform.SetParent(cloth.transform, false);
                hem.AddComponent<MeshFilter>().sharedMesh = CapeHemMesh(width, len);
                var hr = hem.AddComponent<MeshRenderer>();
                hr.sharedMaterial = U.Lit(Color.white, 0.5f);
                v.AddPartRenderer(hr, TrimColor(d));
            }
            pivot.gameObject.AddComponent<CapeSway>().cloth = cloth.transform;
            return pivot.gameObject;
        }

        static readonly Dictionary<long, Mesh> meshCache = new();

        static long MeshKey(int kind, float a, float b) => kind * 100000000L + Mathf.RoundToInt(a * 100f) * 10000L + Mathf.RoundToInt(b * 100f);

        static float CapeZ(float u, float t, float width, float len) => -0.1f * width * (1f - (2f * u - 1f) * (2f * u - 1f)) - 0.18f * len * t * t;

        static Mesh CapeMesh(float width, float len)
        {
            long key = MeshKey(1, width, len);
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int cols = 8, rows = 10;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int j = 0; j <= rows; j++)
            {
                float t = j / (float)rows;
                for (int i = 0; i <= cols; i++)
                {
                    float u = i / (float)cols;
                    float x = (u - 0.5f) * width * (0.8f + 0.45f * t);
                    verts.Add(new Vector3(x, -t * len, CapeZ(u, t, width, len)));
                    uvs.Add(new Vector2(u, 1f - t));
                }
            }
            var tris = new List<int>();
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    int a = j * (cols + 1) + i, b = a + 1, c = a + cols + 1, dd = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(dd);
                }
            var m = DoubleSided("capa", verts, uvs, tris);
            meshCache[key] = m;
            return m;
        }

        static Mesh CapeHemMesh(float width, float len)
        {
            long key = MeshKey(2, width, len);
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int cols = 8;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            float t0 = 0.93f, t1 = 1.0f;
            for (int j = 0; j < 2; j++)
            {
                float t = j == 0 ? t0 : t1;
                for (int i = 0; i <= cols; i++)
                {
                    float u = i / (float)cols;
                    float x = (u - 0.5f) * width * (0.8f + 0.45f * t);
                    verts.Add(new Vector3(x, -t * len, CapeZ(u, t, width, len) - 0.006f));
                    uvs.Add(new Vector2(u, t));
                }
            }
            var tris = new List<int>();
            for (int i = 0; i < cols; i++)
            {
                int a = i, b = i + 1, c = i + cols + 1, dd = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(dd);
            }
            var m = DoubleSided("barra_capa", verts, uvs, tris);
            meshCache[key] = m;
            return m;
        }

        /// <summary>Duplica os triângulos com a face invertida (o URP Lit descarta o verso).</summary>
        static Mesh DoubleSided(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            int n = verts.Count;
            var v2 = new List<Vector3>(verts); v2.AddRange(verts);
            var uv2 = new List<Vector2>(uvs); uv2.AddRange(uvs);
            var t2 = new List<int>(tris);
            for (int i = 0; i < tris.Count; i += 3) { t2.Add(tris[i] + n); t2.Add(tris[i + 2] + n); t2.Add(tris[i + 1] + n); }
            var m = new Mesh { name = name };
            m.SetVertices(v2);
            m.SetUVs(0, uv2);
            m.SetTriangles(t2, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ asas
        static GameObject BuildWings(CharacterVisual v, ItemDef d, BodyInfo bi)
        {
            var tb = bi.torso;
            Vector3 at = new Vector3(0f, tb.center.y + tb.size.y * 0.22f, tb.min.z - 0.05f);
            var pivot = Pivot(v, "asas", v.chestBone != null ? v.chestBone : v.spineBone, at);
            float span = Mathf.Max(0.5f, bi.height * 0.55f), tall = Mathf.Max(0.5f, bi.height * 0.5f);
            Color c = string.IsNullOrEmpty(d.tint) ? Color.white : ItemTint(d);
            string glowHex = GameData.RarityGlow(d);
            Material mat;
            if (!string.IsNullOrEmpty(glowHex))
            {
                Color gl = U.Hex(glowHex) * (d.rarity == "lendario" ? 0.9f : 0.45f);
                mat = U.Lit(Color.white, 0.45f, gl);
            }
            else mat = U.Lit(Color.white, 0.3f);
            var mesh = WingMesh();
            var flap = pivot.gameObject.AddComponent<WingFlap>();
            for (int side = 0; side < 2; side++)
            {
                var hinge = new GameObject(side == 0 ? "asa_dir" : "asa_esq").transform;
                hinge.SetParent(pivot, false);
                hinge.localPosition = new Vector3(side == 0 ? 0.06f : -0.06f, 0f, 0f);
                var w = new GameObject("malha");
                w.transform.SetParent(hinge, false);
                w.transform.localScale = new Vector3(side == 0 ? span : -span, tall, 1f);
                w.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = w.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                v.AddPartRenderer(mr, c);
                if (side == 0) flap.right = hinge; else flap.left = hinge;
            }
            flap.speed = d.rarity == "lendario" ? 3.4f : 2.6f;
            if (d.rarity == "lendario" && !string.IsNullOrEmpty(glowHex))
            {
                var lg = new GameObject("brilho_asas");
                lg.transform.SetParent(pivot, false);
                lg.transform.localPosition = new Vector3(0f, tall * 0.4f, -0.2f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.color = U.Hex(glowHex); l.range = 2.6f; l.intensity = 1.6f; l.shadows = LightShadows.None;
                lg.AddComponent<GlowPulse>();
            }
            return pivot.gameObject;
        }

        /// <summary>Asa direita unitária no plano XY (base na origem, ponta em x=1), com bordas recortadas como penas.</summary>
        static Mesh WingMesh()
        {
            long key = MeshKey(3, 1f, 1f);
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var outline = new Vector2[]
            {
                new Vector2(0.18f, 0.32f), new Vector2(0.45f, 0.62f), new Vector2(0.78f, 0.84f), new Vector2(1.0f, 0.9f),
                new Vector2(0.92f, 0.62f), new Vector2(0.84f, 0.66f), new Vector2(0.76f, 0.34f), new Vector2(0.66f, 0.44f),
                new Vector2(0.58f, 0.08f), new Vector2(0.48f, 0.22f), new Vector2(0.38f, -0.12f), new Vector2(0.28f, 0.04f),
                new Vector2(0.16f, -0.22f), new Vector2(0.08f, -0.04f)
            };
            var verts = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { Vector2.zero };
            foreach (var p in outline)
            {
                // leve curvatura para trás nas pontas
                verts.Add(new Vector3(p.x, p.y, -0.12f * p.x * p.x));
                uvs.Add(p);
            }
            var tris = new List<int>();
            for (int i = 1; i < verts.Count - 1; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }
            var m = DoubleSided("asa", verts, uvs, tris);
            meshCache[key] = m;
            return m;
        }
    }

    /// <summary>Balanço da capa: seno suave + abre mais quando o herói anda.</summary>
    public class CapeSway : MonoBehaviour
    {
        public Transform cloth;
        Vector3 last; float flare; bool init;

        void LateUpdate()
        {
            if (cloth == null) return;
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            Vector3 p = transform.position;
            if (!init) { last = p; init = true; }
            Vector3 vel = (p - last) / dt;
            last = p;
            float fwd = Vector3.Dot(vel, transform.forward);
            float target = Mathf.Clamp(Mathf.Max(0f, fwd) * 5f + new Vector2(vel.x, vel.z).magnitude * 2f, 0f, 32f);
            flare = Mathf.Lerp(flare, target, 1f - Mathf.Exp(-dt * 5f));
            float t = Time.unscaledTime;
            float ang = 3f + 3f * Mathf.Sin(t * 2.1f) + flare;
            float roll = 2f * Mathf.Sin(t * 1.3f + 0.7f);
            cloth.localRotation = Quaternion.Euler(ang, 0f, roll);   // +X gira a barra para trás (-Z local)
        }
    }

    /// <summary>Bater suave das asas (em tempo real, funciona com o jogo pausado na bolsa).</summary>
    public class WingFlap : MonoBehaviour
    {
        public Transform left, right;
        public float speed = 2.6f, amplitude = 16f, spread = 22f;

        void LateUpdate()
        {
            float t = Time.unscaledTime * speed;
            float a = Mathf.Sin(t) * amplitude;
            // abre para trás (Y) e sobe/desce um pouco (Z)
            if (right != null) right.localRotation = Quaternion.Euler(0f, spread + a, 8f + a * 0.35f);
            if (left != null) left.localRotation = Quaternion.Euler(0f, -spread - a, -8f - a * 0.35f);
        }
    }

    // ======================================================================
    /// <summary>
    /// Recolorir cabelo e roupa sem shader: lê a textura atlas do personagem (via Blit + ReadPixels, funciona
    /// mesmo sem Read/Write), classifica cada pixel por matiz/saturação/brilho com uma tabela por personagem
    /// (medida a partir dos UVs de cada malha dos FBX KayKit) e gera uma CÓPIA recolorida que mantém o
    /// sombreamento (cor alvo × brilho do pixel / brilho médio da faixa).
    /// Cabelo só é aplicado na malha *_Head; roupa em todas as malhas do personagem.
    /// Cache por (textura, grupo, cores).
    /// </summary>
    public static class AppearanceTex
    {
        // classe: 1 = cabelo, 2 = roupa primária, 3 = roupa secundária. h em graus (faixa pode dar a volta: h0 > h1).
        struct Rule
        {
            public int cls; public float h0, h1, s0, s1, v0, v1;
            public Rule(int cls, float h0, float h1, float s0, float s1, float v0, float v1)
            { this.cls = cls; this.h0 = h0; this.h1 = h1; this.s0 = s0; this.s1 = s1; this.v0 = v0; this.v1 = v1; }
            public bool Match(float h, float s, float v)
            {
                if (s < s0 || s > s1 || v < v0 || v > v1) return false;
                return h0 <= h1 ? (h >= h0 && h <= h1) : (h >= h0 || h <= h1);
            }
        }

        // Cores medidas nos atlas (amostrando os UVs de cada malha):
        //  Barbarian: cabelo/barba cinza (157,150,142); couro/pele de urso (97,84,76)/(112,97,87); faixas avermelhadas (151,86,66)
        //  Knight:    cabelo loiro (218,174,125); capa/tabardo vermelho (215,44,49); couro (156,91,69)
        //  Mage:      cabelo escuro (29,26,28); túnica roxa (70,66,110)/(29,26,56); capa magenta (165,26,90)
        //  Ranger:    cabelo ruivo (155,90,69); capa azul (38,134,195); túnica cinza-azulada (128,139,144)
        //  Rogue(_Hooded): cabelo castanho (155,90,69); verde (0,140,85)/(9,89,78); couro (171,105,78)/(125,61,44)
        static readonly Dictionary<string, Rule[]> Rules = new()
        {
            ["Barbarian"] = new[]
            {
                new Rule(1, 0f, 360f, 0f, 0.22f, 0.3f, 0.82f),
                new Rule(2, 0f, 45f, 0.1f, 0.36f, 0.2f, 0.56f),
                new Rule(3, 0f, 25f, 0.4f, 0.78f, 0.38f, 0.72f),
            },
            ["Knight"] = new[]
            {
                new Rule(1, 27f, 45f, 0.3f, 0.62f, 0.6f, 0.9f),
                new Rule(2, 340f, 12f, 0.55f, 1f, 0.4f, 1f),
                new Rule(3, 8f, 25f, 0.4f, 0.72f, 0.4f, 0.72f),
            },
            ["Mage"] = new[]
            {
                new Rule(1, 0f, 360f, 0f, 0.3f, 0.07f, 0.3f),
                new Rule(2, 225f, 268f, 0.25f, 0.8f, 0.12f, 0.66f),
                new Rule(3, 305f, 350f, 0.6f, 1f, 0.38f, 0.92f),
            },
            ["Ranger"] = new[]
            {
                new Rule(1, 5f, 25f, 0.4f, 0.75f, 0.4f, 0.75f),
                new Rule(2, 188f, 216f, 0.55f, 1f, 0.45f, 0.95f),
                new Rule(3, 180f, 225f, 0.05f, 0.3f, 0.3f, 0.8f),
            },
            ["Rogue"] = new[]
            {
                new Rule(1, 5f, 25f, 0.4f, 0.75f, 0.4f, 0.75f),
                new Rule(2, 140f, 186f, 0.5f, 1f, 0.2f, 0.8f),
                new Rule(3, 8f, 25f, 0.4f, 0.72f, 0.42f, 0.72f),
            },
        };

        class ClassMap
        {
            public Color32[] src; public byte[] cls; public byte[] ratio; public int w, h;
            public FilterMode filter; public TextureWrapMode wrap;
            public bool[] has = new bool[4];
        }

        static readonly Dictionary<string, ClassMap> maps = new();
        static readonly Dictionary<string, Texture2D> cache = new();
        static readonly List<string> order = new();
        const int MaxCache = 12;

        /// <summary>Cor final a partir do hex salvo e do tom (0..1, 0,5 = neutro). null = cor original.</summary>
        public static Color? Resolve(string hex, float tone)
        {
            if (string.IsNullOrEmpty(hex)) return null;
            return ApplyTone(U.Hex(hex), tone);
        }

        public static Color ApplyTone(Color c, float tone)
        {
            tone = Mathf.Round(Mathf.Clamp01(tone) * 20f) / 20f;   // passos de 5% (menos texturas no cache)
            if (tone < 0.5f) return Color.Lerp(Color.black, c, 0.35f + 1.3f * tone);
            return Color.Lerp(c, Color.white, (tone - 0.5f) * 1.1f);
        }

        static string Prefix(string rendererName)
        {
            if (string.IsNullOrEmpty(rendererName)) return "";
            int us = rendererName.IndexOf('_');
            string p = us > 0 ? rendererName.Substring(0, us) : rendererName;
            if (p == "RogueHooded") p = "Rogue";
            return p;
        }

        /// <summary>Textura recolorida para este renderer (null = usar a original).</summary>
        public static Texture For(Renderer r, Color? hair, Color? cloth1, Color? cloth2)
        {
            if (r == null) return null;
            string prefix = Prefix(r.name);
            if (!Rules.TryGetValue(prefix, out var rules)) return null;
            bool head = r.name.EndsWith("_Head", System.StringComparison.OrdinalIgnoreCase);
            if (!head) hair = null;
            if (!hair.HasValue && !cloth1.HasValue && !cloth2.HasValue) return null;
            var mat = r.sharedMaterial;
            if (mat == null) return null;
            Texture src = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.mainTexture;
            if (src == null) return null;

            string texKey = src.name + "#" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(src);
            string mapKey = texKey + "|" + prefix + "|" + (head ? "cabeca" : "corpo");
            if (!maps.TryGetValue(mapKey, out var cm) || cm == null)
            {
                cm = BuildMap(src, rules, head);
                if (cm == null) return null;
                maps[mapKey] = cm;
            }
            // nenhuma das cores pedidas existe nesta parte -> textura original
            bool any = (hair.HasValue && cm.has[1]) || (cloth1.HasValue && cm.has[2]) || (cloth2.HasValue && cm.has[3]);
            if (!any) return null;
            string key = mapKey + "|" + Hex(hair, cm.has[1]) + "|" + Hex(cloth1, cm.has[2]) + "|" + Hex(cloth2, cm.has[3]);
            if (cache.TryGetValue(key, out var tex) && tex != null)
            {
                order.Remove(key); order.Add(key);
                return tex;
            }
            tex = Recolor(cm, hair, cloth1, cloth2);
            tex.name = "Aparencia_" + prefix + (head ? "_cabeca" : "_corpo");
            cache[key] = tex;
            order.Add(key);
            while (order.Count > MaxCache)
            {
                string old = order[0];
                order.RemoveAt(0);
                if (cache.TryGetValue(old, out var ot) && ot != null) Object.Destroy(ot);
                cache.Remove(old);
            }
            return tex;
        }

        static string Hex(Color? c, bool used) => c.HasValue && used ? ColorUtility.ToHtmlStringRGB(c.Value) : "-";

        static ClassMap BuildMap(Texture src, Rule[] rules, bool head)
        {
            int w = src.width, h = src.height;
            if (w <= 0 || h <= 0) return null;
            Color32[] px;
            try
            {
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var prev = RenderTexture.active;
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                t.Apply(false);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                px = t.GetPixels32();
                Object.Destroy(t);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Drakantus] Aparência: não foi possível ler a textura " + src.name + ": " + e.Message);
                return null;
            }
            var cm = new ClassMap { src = px, w = w, h = h, cls = new byte[px.Length], ratio = new byte[px.Length], filter = src.filterMode, wrap = src.wrapMode };
            var vals = new float[px.Length];
            double[] sum = new double[4]; int[] cnt = new int[4];
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                Color.RGBToHSV(new Color(p.r / 255f, p.g / 255f, p.b / 255f), out float hh, out float s, out float v);
                hh *= 360f;
                int k = 0;
                for (int r = 0; r < rules.Length; r++)
                {
                    if (rules[r].cls == 1 && !head) continue;
                    if (rules[r].Match(hh, s, v)) { k = rules[r].cls; break; }
                }
                cm.cls[i] = (byte)k;
                vals[i] = v;
                if (k > 0) { sum[k] += v; cnt[k]++; }
            }
            var refV = new float[4];
            for (int k = 1; k < 4; k++) { refV[k] = cnt[k] > 0 ? Mathf.Max(0.05f, (float)(sum[k] / cnt[k])) : 1f; cm.has[k] = cnt[k] > 0; }
            for (int i = 0; i < px.Length; i++)
            {
                int k = cm.cls[i];
                if (k == 0) continue;
                cm.ratio[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(vals[i] / refV[k] * 100f), 0, 255);
            }
            return cm;
        }

        static Texture2D Recolor(ClassMap cm, Color? hair, Color? c1, Color? c2)
        {
            var outPx = (Color32[])cm.src.Clone();
            var targets = new Color[4];
            var on = new bool[4];
            if (hair.HasValue) { targets[1] = hair.Value; on[1] = true; }
            if (c1.HasValue) { targets[2] = c1.Value; on[2] = true; }
            if (c2.HasValue) { targets[3] = c2.Value; on[3] = true; }
            for (int i = 0; i < outPx.Length; i++)
            {
                int k = cm.cls[i];
                if (k == 0 || !on[k]) continue;
                float f = cm.ratio[i] / 100f;
                var t = targets[k];
                outPx[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(t.r * f * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(t.g * f * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(t.b * f * 255f), 0, 255),
                    outPx[i].a);
            }
            var tex = new Texture2D(cm.w, cm.h, TextureFormat.RGBA32, true, false);
            tex.filterMode = cm.filter;
            tex.wrapMode = cm.wrap;
            tex.SetPixels32(outPx);
            tex.Apply(true, true);
            return tex;
        }
    }
}
