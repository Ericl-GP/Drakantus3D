using System.Collections.Generic;
using UnityEngine;

namespace Drakantus.EditorTools
{
    /// <summary>
    /// Peças prontas do andar (usadas pelo gerador E pela paleta de retoque): tochas, braseiros, portas,
    /// alavancas, armadilhas, névoa, baús, marcadores de inimigo, santuário, escadas, plataformas,
    /// pisos, paredes e decoração do bioma (inclusive formas primitivas "#duna", "#cristal"...).
    /// Posições são LOCAIS ao parent. Bioma atual em <see cref="biome"/>.
    /// </summary>
    public static class FloorPieces
    {
        public static BiomeDef biome;
        public static System.Random rng = new System.Random(1);

        static float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public static Color Hex(string h, Color fb) => FloorKit.Hex(h, fb);
        static BiomeDef B => biome ?? new BiomeDef();
        public static Color TorchColor => Hex(B.torchColor, new Color(1f, 0.62f, 0.3f));
        public static Color FlameColor(Color torch) => Color.Lerp(new Color(1f, 0.55f, 0.15f), torch, 0.5f);

        // ================================================================== chão e paredes
        public static GameObject FloorTile(Transform parent, Vector3 center, float top, float size, bool alt)
        {
            string id = alt && !string.IsNullOrEmpty(B.floorBlockAlt) ? B.floorBlockAlt : B.floorBlock;
            Color tint = alt && !string.IsNullOrEmpty(B.floorAltTint) ? Hex(B.floorAltTint, Color.white) : Hex(B.floorTint, Color.white);
            float thick = Mathf.Max(0.5f, top + 0.5f);
            return FloorKit.Box(id, parent, new Vector3(center.x, top - thick * 0.5f, center.z), new Vector3(size, thick, size), 90f * rng.Next(0, 4), new Color(0.45f, 0.45f, 0.48f), tint);
        }

        public static GameObject WallBlock(Transform parent, Vector3 center, float baseY, float top, float size, bool occluder)
        {
            string id = rng.NextDouble() < 0.22 && !string.IsNullOrEmpty(B.wallBlockAlt) ? B.wallBlockAlt : B.wallBlock;
            float h = Mathf.Max(0.3f, top - baseY);
            var g = FloorKit.Box(id, parent, new Vector3(center.x, baseY + h * 0.5f, center.z), new Vector3(size, h, size), 90f * rng.Next(0, 4), new Color(0.5f, 0.42f, 0.38f), Hex(B.wallTint, Color.white));
            FloorKit.AddCollider(g, 1f, 0f);
            if (occluder) FloorKit.Occluder(g);
            return g;
        }

        // ================================================================== decoração
        public static DecoDef PickDeco(DecoDef[] list)
        {
            if (list == null || list.Length == 0) return null;
            float total = 0f;
            foreach (var d in list) if (d != null) total += Mathf.Max(0.01f, d.weight);
            float r = Rf(0f, total);
            foreach (var d in list)
            {
                if (d == null) continue;
                r -= Mathf.Max(0.01f, d.weight);
                if (r <= 0f) return d;
            }
            return list[list.Length - 1];
        }

        /// <summary>Coloca um item de decoração (modelo ou primitiva "#...").</summary>
        public static GameObject Deco(Transform parent, DecoDef d, Vector3 pos, float yaw)
        {
            if (d == null || string.IsNullOrEmpty(d.id)) return null;
            float h = Rf(d.min, Mathf.Max(d.min, d.max));
            Color tint = !string.IsNullOrEmpty(d.tint) ? Hex(d.tint, Color.white) : Hex(B.decoTint, Color.white);
            GameObject g;
            if (d.id.StartsWith("#")) g = Primitive(parent, d.id.Substring(1), pos, yaw, h, tint);
            else if (d.lying) g = FloorKit.Lying(d.id, parent, pos, yaw, h, tint);
            else g = FloorKit.Sized(d.id, parent, pos, yaw, h, tint);
            if (g == null) return null;
            if (d.collider && g.GetComponent<Collider>() == null && g.GetComponentInChildren<Collider>() == null)
            {
                string k = d.id.ToLowerInvariant();
                if (k.StartsWith("tree"))
                    FloorKit.ColliderBox(g.transform.parent, pos + Vector3.up * 1.5f, new Vector3(0.8f, 3f, 0.8f), 0f, "Tronco");
                else FloorKit.AddCollider(g, 0.8f, 1.2f);
            }
            if (d.occluder) FloorKit.Occluder(g);
            return g;
        }

        /// <summary>Formas simples para biomas sem modelos próprios. TODO(modelos): trocar por modelos de verdade.</summary>
        public static GameObject Primitive(Transform parent, string kind, Vector3 pos, float yaw, float h, Color tint)
        {
            var root = FloorKit.Empty(parent, "#" + kind, pos, yaw);
            var t = root.transform;
            switch (kind)
            {
                case "duna":
                {
                    // TODO(modelos): dunas do Quaternius Nature
                    Color sand = new Color(0.93f, 0.8f, 0.58f) * tint;
                    FloorKit.Prim(PrimitiveType.Sphere, t, "duna", new Vector3(0, 0, 0), new Vector3(h * 6f, h * 2f, h * 4.2f), Quaternion.identity, FloorKit.Mat(sand, 0.05f), false);
                    FloorKit.Prim(PrimitiveType.Sphere, t, "duna2", new Vector3(h * 2f, -h * 0.2f, h * 1.2f), new Vector3(h * 4f, h * 1.4f, h * 3f), Quaternion.Euler(0, 30, 0), FloorKit.Mat(sand * 0.95f, 0.05f), false);
                    break;
                }
                case "monte_neve":
                    FloorKit.Prim(PrimitiveType.Sphere, t, "neve", Vector3.zero, new Vector3(h * 4f, h * 2f, h * 3f), Quaternion.identity, FloorKit.Mat(new Color(0.95f, 0.97f, 1f) * tint, 0.3f), false);
                    break;
                case "cristal":
                {
                    // TODO(modelos): cristais de gelo do Kenney Holiday
                    var ice = FloorKit.Mat(new Color(0.65f, 0.85f, 1f, 0.72f) * tint, 0.9f, new Color(0.1f, 0.25f, 0.4f), true);
                    int n = 3;
                    for (int i = 0; i < n; i++)
                    {
                        float hh = h * (i == 0 ? 1f : Rf(0.45f, 0.75f));
                        float w = hh * 0.28f;
                        var c = FloorKit.Cube(t, "cristal", new Vector3(i == 0 ? 0 : Rf(-0.5f, 0.5f), hh * 0.45f, i == 0 ? 0 : Rf(-0.5f, 0.5f)), new Vector3(w, hh, w),
                            Quaternion.Euler(Rf(-14f, 14f), Rf(0f, 90f), Rf(-14f, 14f)) * Quaternion.Euler(0, 45, 0), ice, false);
                        c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    FloorKit.ColliderBox(t, new Vector3(0, h * 0.5f, 0), new Vector3(h * 0.4f, h, h * 0.4f));
                    break;
                }
                case "escombro":
                {
                    Color st = new Color(0.55f, 0.52f, 0.5f) * tint;
                    int n = rng.Next(2, 5);
                    for (int i = 0; i < n; i++)
                    {
                        float s = h * Rf(0.4f, 1f);
                        var c = FloorKit.Box(rng.NextDouble() < 0.5 ? "bricks_A" : "stone", t, new Vector3(Rf(-0.8f, 0.8f), s * 0.35f, Rf(-0.8f, 0.8f)), new Vector3(s, s * 0.7f, s * Rf(0.6f, 1.2f)), Rf(0f, 360f), st, tint);
                        c.transform.localRotation = Quaternion.Euler(Rf(-25f, 25f), Rf(0f, 360f), Rf(-25f, 25f));
                    }
                    FloorKit.ColliderBox(t, new Vector3(0, h * 0.4f, 0), new Vector3(h * 1.2f, h * 0.8f, h * 1.2f));
                    break;
                }
                case "tumulo":
                {
                    Color st = new Color(0.62f, 0.62f, 0.66f) * tint;
                    var m = FloorKit.Mat(st, 0.1f);
                    FloorKit.Cube(t, "base", new Vector3(0, 0.08f, 0.35f), new Vector3(1.0f, 0.16f, 1.9f), Quaternion.identity, FloorKit.Mat(st * 0.8f, 0.05f), false);
                    FloorKit.Cube(t, "lapide", new Vector3(0, h * 0.5f, -0.45f), new Vector3(0.75f, h, 0.22f), Quaternion.Euler(Rf(-6f, 6f), 0, Rf(-5f, 5f)), m, false);
                    FloorKit.Prim(PrimitiveType.Cylinder, t, "topo", new Vector3(0, h, -0.45f), new Vector3(0.75f, 0.11f, 0.75f), Quaternion.Euler(90, 0, 0), m, false);
                    FloorKit.ColliderBox(t, new Vector3(0, 0.5f, 0.2f), new Vector3(1f, 1f, 2f));
                    break;
                }
                case "cruz":
                {
                    var m = FloorKit.Mat(new Color(0.55f, 0.55f, 0.6f) * tint, 0.1f);
                    FloorKit.Cube(t, "haste", new Vector3(0, h * 0.5f, 0), new Vector3(0.22f, h, 0.22f), Quaternion.identity, m, false);
                    FloorKit.Cube(t, "braco", new Vector3(0, h * 0.72f, 0), new Vector3(0.9f, 0.2f, 0.22f), Quaternion.identity, m, false);
                    FloorKit.ColliderBox(t, new Vector3(0, h * 0.5f, 0), new Vector3(0.5f, h, 0.5f));
                    break;
                }
                case "cacto":
                {
                    var m = FloorKit.Mat(new Color(0.35f, 0.6f, 0.3f) * tint, 0.2f);
                    FloorKit.Prim(PrimitiveType.Capsule, t, "tronco", new Vector3(0, h * 0.5f, 0), new Vector3(0.45f, h * 0.5f, 0.45f), Quaternion.identity, m, false);
                    FloorKit.Prim(PrimitiveType.Capsule, t, "braco1", new Vector3(0.38f, h * 0.55f, 0), new Vector3(0.3f, h * 0.2f, 0.3f), Quaternion.identity, m, false);
                    FloorKit.Prim(PrimitiveType.Capsule, t, "braco2", new Vector3(-0.36f, h * 0.68f, 0), new Vector3(0.28f, h * 0.17f, 0.28f), Quaternion.identity, m, false);
                    FloorKit.ColliderBox(t, new Vector3(0, h * 0.5f, 0), new Vector3(0.7f, h, 0.7f));
                    break;
                }
                case "pilar_quebrado":
                {
                    Color st = Hex(B.wallTint, Color.white) * tint;
                    FloorKit.Box("stone_dark", t, new Vector3(0, 0.2f, 0), new Vector3(1.8f, 0.4f, 1.8f), 0f, new Color(0.3f, 0.3f, 0.34f), st);
                    var col = FloorKit.Box("bricks_B", t, new Vector3(0, 0.4f + h * 0.5f, 0), new Vector3(1.2f, h, 1.2f), 0f, new Color(0.55f, 0.45f, 0.4f), st);
                    var chunk = FloorKit.Box("bricks_B", t, new Vector3(1.3f, 0.35f, Rf(-0.6f, 0.6f)), new Vector3(1.1f, 0.7f, 1.0f), Rf(0f, 360f), new Color(0.55f, 0.45f, 0.4f), st);
                    chunk.transform.localRotation = Quaternion.Euler(Rf(-20f, 20f), Rf(0, 360), 80f);
                    FloorKit.ColliderBox(t, new Vector3(0, (0.4f + h) * 0.5f, 0), new Vector3(1.6f, 0.4f + h, 1.6f));
                    break;
                }
                case "estalagmite":
                {
                    var m = FloorKit.Mat(new Color(0.42f, 0.4f, 0.44f) * tint, 0.15f);
                    float y = 0f;
                    for (int i = 0; i < 3; i++)
                    {
                        float seg = h / 3f;
                        float w = (1f - i * 0.3f) * h * 0.35f;
                        FloorKit.Cube(t, "pedra", new Vector3(0, y + seg * 0.5f, 0), new Vector3(w, seg * 1.05f, w), Quaternion.Euler(0, 45 + i * 20, Rf(-4f, 4f)), m, false);
                        y += seg;
                    }
                    FloorKit.ColliderBox(t, new Vector3(0, h * 0.5f, 0), new Vector3(h * 0.3f, h, h * 0.3f));
                    break;
                }
                case "lava":
                {
                    float r = Mathf.Max(1.2f, h * 1.6f);
                    FloorKit.Prim(PrimitiveType.Cylinder, t, "lava", new Vector3(0, 0.03f, 0), new Vector3(r * 2f, 0.03f, r * 2f), Quaternion.identity,
                        FloorKit.Mat(new Color(1f, 0.42f, 0.08f), 0.6f, new Color(2.2f, 0.75f, 0.12f)), false);
                    FloorKit.Prim(PrimitiveType.Cylinder, t, "borda", new Vector3(0, 0.02f, 0), new Vector3(r * 2.3f, 0.02f, r * 2.3f), Quaternion.identity,
                        FloorKit.Mat(new Color(0.15f, 0.1f, 0.08f), 0.1f), false);
                    FloorKit.ColliderBox(t, new Vector3(0, 1f, 0), new Vector3(r * 1.5f, 2f, r * 1.5f), 0f, "Colisor_Lava");
                    FloorKit.Fx(t, new Vector3(0, 0.6f, 0), FloorFxKind.Brasas, new Vector3(r * 2f, 1f, r * 2f), new Color(1f, 0.5f, 0.15f), 14);
                    break;
                }
                default:
                    FloorKit.Cube(t, kind, new Vector3(0, h * 0.5f, 0), new Vector3(h * 0.6f, h, h * 0.6f), Quaternion.identity, FloorKit.Mat(Color.gray * tint), false);
                    break;
            }
            return root;
        }

        // ================================================================== luzes
        /// <summary>Tocha presa na parede (face da parede em wallPoint, dir = para dentro da sala).</summary>
        public static GameObject WallTorch(Transform parent, Vector3 wallPoint, Vector3 dir, float floorY)
        {
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var root = FloorKit.Empty(parent, "Tocha_Parede", wallPoint + dir * 0.15f + Vector3.up * floorY, yaw);
            var t = root.transform;
            FloorKit.Cube(t, "suporte", new Vector3(0, 1.7f, -0.05f), new Vector3(0.12f, 0.12f, 0.35f), Quaternion.identity, FloorKit.Mat(new Color(0.17f, 0.16f, 0.19f)), false);
            FloorKit.Sized("torch", t, new Vector3(0, 1.35f, 0.15f), 0f, 1.0f, Color.white, 18f);
            Color c = TorchColor;
            FloorKit.Fx(t, new Vector3(0, 2.3f, 0.45f), FloorFxKind.Chama, Vector3.one, FlameColor(c), 12, 0.8f);
            FloorKit.PointLight(t, new Vector3(0, 2.45f, 0.9f), c, 8.5f, 2.6f, true);
            return root;
        }

        /// <summary>Tocha de pé.</summary>
        public static GameObject StandingTorch(Transform parent, Vector3 pos)
        {
            var root = FloorKit.Empty(parent, "Tocha", pos);
            var t = root.transform;
            FloorKit.Sized("torch", t, Vector3.zero, 0f, 1.7f, Color.white);
            Color c = TorchColor;
            FloorKit.Fx(t, new Vector3(0, 1.75f, 0), FloorFxKind.Chama, Vector3.one, FlameColor(c), 14, 1f);
            FloorKit.PointLight(t, new Vector3(0, 2.1f, 0), c, 7.5f, 2.6f, true);
            return root;
        }

        /// <summary>Braseiro (balde de metal com fogo). shadows = spot com sombra (máx. 2 por andar).</summary>
        public static GameObject Brazier(Transform parent, Vector3 pos, bool shadows)
        {
            var root = FloorKit.Empty(parent, "Braseiro", pos);
            var t = root.transform;
            var g = FloorKit.Sized("bucket_metal", t, Vector3.zero, 0f, 0.95f, Color.white);
            FloorKit.AddCollider(g, 0.8f, 1f);
            Color c = TorchColor;
            FloorKit.Fx(t, new Vector3(0, 1.0f, 0), FloorFxKind.Chama, Vector3.one, FlameColor(c), 20, 1.6f);
            FloorKit.PointLight(t, new Vector3(0, 1.8f, 0), c, 9f, 3.2f, true, shadows);
            return root;
        }

        public static GameObject Lantern(Transform parent, Vector3 pos)
        {
            var root = FloorKit.Empty(parent, "Poste", pos);
            var t = root.transform;
            FloorKit.Prim(PrimitiveType.Cylinder, t, "poste", new Vector3(0, 1.4f, 0), new Vector3(0.18f, 1.4f, 0.18f), Quaternion.identity, FloorKit.Mat(new Color(0.24f, 0.18f, 0.13f)), true);
            FloorKit.Sized("lantern", t, new Vector3(0, 2.75f, 0), 0f, 0.7f, Color.white);
            FloorKit.PointLight(t, new Vector3(0, 3.1f, 0), TorchColor, 7f, 1.6f, false);
            return root;
        }

        public static GameObject AmbientLight(Transform parent, Vector3 pos, Color c, float range, float intensity)
        {
            var l = FloorKit.PointLight(parent, pos + Vector3.up * 3f, c, range, intensity, false);
            l.gameObject.name = "Luz_Ambiente";
            return l.gameObject;
        }

        // ================================================================== armadilhas
        static void Spikes(Transform parent, float size, float downY)
        {
            var sp = FloorKit.Empty(parent, "espetos", new Vector3(0, downY, 0));
            var m = FloorKit.Mat(new Color(0.62f, 0.62f, 0.66f), 0.7f);
            int n = Mathf.Clamp(Mathf.RoundToInt(size / 0.55f), 2, 6);
            float step = size / n;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    Vector3 p = new Vector3(-size * 0.5f + step * (i + 0.5f), 0.3f, -size * 0.5f + step * (j + 0.5f));
                    FloorKit.Cube(sp.transform, "espeto", p, new Vector3(0.1f, 0.6f, 0.1f), Quaternion.Euler(0, 45, 0), m, false);
                }
        }

        public static GameObject MakeSpikes(Transform parent, Vector3 pos, bool hidden, float size, float damage)
        {
            var root = FloorKit.Empty(parent, hidden ? "Espetos_Escondidos" : "Espetos", pos);
            if (!hidden)
                FloorKit.Cube(root.transform, "grade", new Vector3(0, 0.025f, 0), new Vector3(size, 0.05f, size), Quaternion.identity, FloorKit.Mat(new Color(0.2f, 0.19f, 0.22f), 0.4f), false);
            Spikes(root.transform, size, -0.75f);
            var st = root.AddComponent<Drakantus.SpikeTrap>();
            st.hidden = hidden;
            st.size = size;
            st.damage = damage;
            st.spikes = root.transform.Find("espetos");
            st.offset = Rf(0f, 3f);
            return root;
        }

        public static GameObject MakeFireTrap(Transform parent, Vector3 pos, float damage, bool signalOnly)
        {
            var root = FloorKit.Empty(parent, "Jato_de_Fogo", pos);
            var t = root.transform;
            FloorKit.Cube(t, "grelha", new Vector3(0, 0.04f, 0), new Vector3(1.4f, 0.08f, 1.4f), Quaternion.identity, FloorKit.Mat(new Color(0.15f, 0.13f, 0.12f), 0.3f), false);
            FloorKit.Cube(t, "brilho", new Vector3(0, 0.085f, 0), new Vector3(0.7f, 0.02f, 0.7f), Quaternion.identity, FloorKit.Mat(new Color(1f, 0.4f, 0.1f), 0.5f, new Color(1.2f, 0.35f, 0.05f)), false);
            var f = root.AddComponent<Drakantus.FireTrap>();
            f.damage = damage;
            f.signalOnly = signalOnly;
            f.offset = Rf(0f, 4f);
            return root;
        }

        /// <summary>Lançador de flechas: pos = face da parede, dir = para onde atira.</summary>
        public static GameObject MakeArrowTrap(Transform parent, Vector3 pos, Vector3 dir, float damage, bool signalOnly)
        {
            dir.y = 0;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var root = FloorKit.Empty(parent, "Lancador_Flechas", pos, yaw);
            var t = root.transform;
            FloorKit.Cube(t, "caixa", new Vector3(0, 0, -0.1f), new Vector3(0.6f, 0.45f, 0.3f), Quaternion.identity, FloorKit.Mat(new Color(0.3f, 0.28f, 0.3f), 0.2f), false);
            FloorKit.Cube(t, "fenda", new Vector3(0, 0, 0.06f), new Vector3(0.4f, 0.08f, 0.04f), Quaternion.identity, FloorKit.Mat(new Color(0.05f, 0.04f, 0.04f)), false);
            var a = root.AddComponent<Drakantus.ArrowTrap>();
            a.damage = damage;
            a.signalOnly = signalOnly;
            a.offset = Rf(0f, 2f);
            return root;
        }

        public static GameObject MakePlate(Transform parent, Vector3 pos, bool once)
        {
            var root = FloorKit.Empty(parent, "Placa_de_Pressao", pos);
            var plate = FloorKit.Cube(root.transform, "placa", new Vector3(0, 0.04f, 0), new Vector3(1.5f, 0.08f, 1.5f), Quaternion.identity, FloorKit.Mat(new Color(0.48f, 0.44f, 0.38f), 0.2f), false);
            var p = root.AddComponent<Drakantus.PressurePlate>();
            p.radius = 0.9f;
            p.once = once;
            p.plate = plate.transform;
            return root;
        }

        // [Quebraveis]
        /// <summary>Objeto quebrável (barrel, crate, crate_small, vase, explosive). O visual é montado ao jogar (Breakable.Start).</summary>
        public static GameObject MakeBreakable(Transform parent, Vector3 pos, string kind, float yaw = 0f)
        {
            var root = FloorKit.Empty(parent, "Quebravel_" + kind, pos, yaw);
            var b = root.AddComponent<Drakantus.Breakable>();
            b.kind = kind;
            return root;
        }

        public static GameObject MakeFog(Transform parent, Vector3 pos, float radius, float dps)
        {
            var root = FloorKit.Empty(parent, "Nevoa_Venenosa", pos);
            var f = root.AddComponent<Drakantus.PoisonFog>();
            f.radius = radius;
            f.dps = dps;
            f.pulsePeriod = rng.NextDouble() < 0.5 ? 0f : Rf(5f, 8f);
            return root;
        }

        // ================================================================== portas e alavancas
        /// <summary>
        /// Porta no centro (local) pos virada para dir (eixo do corredor). style: "grade", "pedra", "secreta".
        /// widthCells = 1 ou 2 (corredor largo: o segundo bloco fica do lado +side).
        /// </summary>
        public static Drakantus.Door MakeDoor(Transform parent, Vector3 pos, Vector3 dir, Vector3 side, float width, float height, string style, bool startOpen)
        {
            dir.y = 0;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var root = FloorKit.Empty(parent, style == "secreta" ? "Parede_Secreta" : style == "grade" ? "Porta_Grade" : "Porta_Pedra", pos, yaw);
            var t = root.transform;
            var panel = FloorKit.Empty(t, "painel", Vector3.zero);
            var pt = panel.transform;
            if (style == "grade")
            {
                var m = FloorKit.Mat(new Color(0.18f, 0.17f, 0.2f), 0.6f);
                int bars = Mathf.Max(3, Mathf.RoundToInt(width / 0.45f));
                for (int i = 0; i < bars; i++)
                {
                    float x = -width * 0.5f + (i + 0.5f) * width / bars;
                    FloorKit.Cube(pt, "barra", new Vector3(x, height * 0.5f, 0), new Vector3(0.1f, height, 0.1f), Quaternion.identity, m, false);
                }
                FloorKit.Cube(pt, "travessa", new Vector3(0, height - 0.2f, 0), new Vector3(width, 0.14f, 0.14f), Quaternion.identity, m, false);
                FloorKit.Cube(pt, "travessa2", new Vector3(0, height * 0.45f, 0), new Vector3(width, 0.12f, 0.12f), Quaternion.identity, m, false);
            }
            else if (style == "secreta")
            {
                // igual à parede (um pouco mais escura e rachada)
                var g = FloorKit.Box(B.wallBlock, pt, new Vector3(0, height * 0.5f, 0), new Vector3(width, height, FloorKit.TILE), 0f, new Color(0.5f, 0.42f, 0.38f), Hex(B.wallTint, Color.white) * 0.86f);
                FloorKit.Occluder(g);
            }
            else
            {
                FloorKit.Box("stone_dark", pt, new Vector3(0, height * 0.5f, 0), new Vector3(width, height, 0.7f), 0f, new Color(0.3f, 0.3f, 0.34f), Color.white);
                FloorKit.Box("metal", pt, new Vector3(0, height * 0.5f, -0.38f), new Vector3(width * 0.3f, height * 0.6f, 0.08f), 0f, new Color(0.4f, 0.4f, 0.45f), Color.white);
            }
            var bc = panel.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, height * 0.5f, 0);
            bc.size = new Vector3(width, height, style == "secreta" ? FloorKit.TILE : 0.7f);
            var d = root.AddComponent<Drakantus.Door>();
            d.panel = pt;
            d.startOpen = startOpen;
            d.openDepth = height + 0.15f;
            d.secret = style == "secreta";
            if (style == "pedra" && !startOpen) d.lockedHint = "Porta trancada — procure uma alavanca";
            return d;
        }

        /// <summary>Alavanca encostada na parede (dir = para dentro da sala).</summary>
        public static Drakantus.Lever MakeLever(Transform parent, Vector3 pos, Vector3 dir)
        {
            dir.y = 0;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var root = FloorKit.Empty(parent, "Alavanca", pos, yaw);
            var t = root.transform;
            var stone = FloorKit.Mat(new Color(0.4f, 0.38f, 0.4f), 0.1f);
            FloorKit.Cube(t, "base", new Vector3(0, 0.3f, 0), new Vector3(0.7f, 0.6f, 0.5f), Quaternion.identity, stone, true);
            var pivot = FloorKit.Empty(t, "alavanca", new Vector3(0, 0.6f, 0.05f));
            FloorKit.Cube(pivot.transform, "haste", new Vector3(0, 0.4f, 0), new Vector3(0.09f, 0.8f, 0.09f), Quaternion.identity, FloorKit.Mat(new Color(0.35f, 0.24f, 0.15f)), false);
            FloorKit.Prim(PrimitiveType.Sphere, pivot.transform, "punho", new Vector3(0, 0.82f, 0), Vector3.one * 0.18f, Quaternion.identity, FloorKit.Mat(new Color(0.9f, 0.25f, 0.2f), 0.4f), false);
            var lv = root.AddComponent<Drakantus.Lever>();
            lv.handle = pivot.transform;
            lv.once = true;
            return lv;
        }

        /// <summary>Liga source (com TriggerLink) aos alvos.</summary>
        public static TriggerLink Link(GameObject source, params FloorReceiver[] targets)
        {
            var l = source.GetComponent<TriggerLink>();
            if (l == null) l = source.AddComponent<TriggerLink>();
            foreach (var t in targets) if (t != null && !l.targets.Contains(t)) l.targets.Add(t);
            return l;
        }

        public static RoomClearTrigger MakeRoomClear(Transform parent, Vector3 center, Vector3 size)
        {
            var root = FloorKit.Empty(parent, "Gatilho_Arena", center);
            var r = root.AddComponent<RoomClearTrigger>();
            r.size = size;
            return r;
        }

        // ================================================================== baús, inimigos, santuário, portais
        public static ChestMarker MakeChest(Transform parent, Vector3 pos, int tier, bool waitSignal)
        {
            var root = FloorKit.Empty(parent, tier >= 2 ? "Bau_Raro" : "Bau", pos);
            var c = root.AddComponent<ChestMarker>();
            c.tier = tier;
            c.waitSignal = waitSignal;
            return c;
        }

        public static EnemySpawnMarker MakeEnemy(Transform parent, Vector3 pos, string id, EnemyRole role, string[] abilities, float scale = 1f)
        {
            var root = FloorKit.Empty(parent, (role == EnemyRole.Chefe ? "Chefe_" : role == EnemyRole.MiniChefe ? "MiniChefe_" : role == EnemyRole.Elite ? "Elite_" : "Inimigo_") + id, pos, Rf(0f, 360f));
            var m = root.AddComponent<EnemySpawnMarker>();
            m.enemyId = id;
            m.role = role;
            m.scale = scale;
            m.abilities = abilities ?? new string[0];
            return m;
        }

        public static GameObject MakeSanctuary(Transform parent, Vector3 pos)
        {
            var root = FloorKit.Empty(parent, "Santuario", pos);
            root.AddComponent<SanctuaryMarker>();
            var t = root.transform;
            FloorKit.Prim(PrimitiveType.Cylinder, t, "circulo", new Vector3(0, 0.03f, 0), new Vector3(6f, 0.03f, 6f), Quaternion.identity, FloorKit.Mat(new Color(0.55f, 0.75f, 0.5f), 0.3f, new Color(0.08f, 0.2f, 0.12f)), false);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 4f, 0, Mathf.Sin(a) * 4f);
                FloorKit.Box("stone", t, p + Vector3.up * 0.6f, new Vector3(0.6f, 1.2f, 0.6f), a * Mathf.Rad2Deg, new Color(0.6f, 0.6f, 0.62f), Hex(B.wallTint, Color.white));
            }
            FloorKit.PointLight(t, new Vector3(0, 3f, 0), new Color(0.65f, 0.95f, 0.85f), 11f, 2.4f, false);
            FloorKit.Fx(t, new Vector3(0, 1.5f, 0), FloorFxKind.Vagalumes, new Vector3(7f, 3f, 7f), new Color(0.6f, 1f, 0.9f), 30);
            return root;
        }

        public static GameObject MakeSpawn(Transform parent, Vector3 pos, string key)
        {
            var root = FloorKit.Empty(parent, "Entrada_" + key, pos);
            root.AddComponent<FloorSpawnPoint>().key = key;
            return root;
        }

        public static GameObject MakePortal(Transform parent, Vector3 pos, string label = "Sair da Torre", string map = "town", string spawn = "tower_door")
        {
            var root = FloorKit.Empty(parent, "Portal_Saida", pos);
            var p = root.AddComponent<FloorPortalMarker>();
            p.label = label; p.targetMap = map; p.targetSpawn = spawn;
            return root;
        }

        // ================================================================== escadas e plataformas
        /// <summary>
        /// Escada de blocos: pé em pos (local), sobe "height" no sentido +Z do yaw. Degraus de 0,3 m (o
        /// CharacterController sobe sozinho) e uma rampa invisível por cima para andar liso.
        /// </summary>
        public static GameObject MakeStairs(Transform parent, Vector3 pos, float yaw, float height, float width, bool steps = true)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(height / 0.3f));
            float rise = height / n, tread = 0.6f, len = n * tread;
            var root = FloorKit.Empty(parent, steps ? "Escada" : "Rampa", pos, yaw);
            var t = root.transform;
            Color tint = Hex(B.floorTint, Color.white);
            string id = string.IsNullOrEmpty(B.floorBlock) ? "stone" : B.floorBlock;
            if (steps)
            {
                for (int i = 0; i < n; i++)
                {
                    float h = (i + 1) * rise;
                    FloorKit.Box(i % 2 == 0 ? id : (string.IsNullOrEmpty(B.floorBlockAlt) ? id : B.floorBlockAlt), t,
                        new Vector3(0, h * 0.5f - 0.25f, (i + 0.5f) * tread), new Vector3(width, h + 0.5f, tread), 0f, new Color(0.5f, 0.5f, 0.52f), tint);
                }
            }
            else
            {
                var r = FloorKit.Box(id, t, new Vector3(0, height * 0.5f - 0.25f, len * 0.5f), new Vector3(width, height + 0.5f, len), 0f, new Color(0.5f, 0.5f, 0.52f), tint);
                r.name = "base_rampa";
            }
            // rampa invisível (colisão)
            float slope = Mathf.Sqrt(len * len + height * height);
            float pitch = -Mathf.Atan2(height, len) * Mathf.Rad2Deg;
            var col = FloorKit.Empty(t, "Colisor_Rampa", new Vector3(0, height * 0.5f + (steps ? rise * 0.5f : 0f), len * 0.5f));
            col.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            var bc = col.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, -0.1f, 0);
            bc.size = new Vector3(width, 0.2f, slope + 0.1f);
            var fs = root.AddComponent<FloorStairs>();
            fs.height = height; fs.length = len; fs.width = width; fs.steps = steps;
            return root;
        }

        /// <summary>Plataforma elevada (w x d, altura h) com escada no lado -Z local. pos = centro na base.</summary>
        public static GameObject MakePlatform(Transform parent, Vector3 pos, float yaw, float w, float d, float h)
        {
            var root = FloorKit.Empty(parent, "Plataforma", pos, yaw);
            var t = root.transform;
            var top = FloorKit.Box(string.IsNullOrEmpty(B.floorBlock) ? "stone" : B.floorBlock, t, new Vector3(0, (h - 0.5f) * 0.5f, 0), new Vector3(w, h + 0.5f, d), 0f, new Color(0.5f, 0.5f, 0.52f), Hex(B.floorTint, Color.white));
            FloorKit.AddCollider(top, 1f, 0f);
            float len = Mathf.CeilToInt(h / 0.3f) * 0.6f;
            MakeStairs(t, new Vector3(0, 0, -d * 0.5f - len), 0f, h, Mathf.Min(4f, w));
            return root;
        }
    }
}
