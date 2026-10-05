using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Pets da Guilda (loja da Lumi, Resources/Data/pets.json). Um pet ativo segue o herói entre mapas,
    /// busca o loot do chão, ganha XP (níveis 1–10), ataca a partir do nível 3, ganha habilidade
    /// especial no nível 6 e evolui (maior, com aura e coroa) no nível 10.
    /// </summary>
    public static class Pets
    {
        public const int MaxLevel = 10, AttackLevel = 3, SpecialLevel = 6;
        public static Pet Current;

        static GuildProfile G => GuildState.G;

        public static OwnedPet ActiveOwned => G.activePet >= 0 ? G.pets.Find(p => p.uid == G.activePet) : null;
        public static OwnedPet Find(int uid) => G.pets.Find(p => p.uid == uid);
        public static bool Owns(string petId) => G.pets.Exists(p => p.petId == petId);
        public static int XpToNext(int level) => 20 + level * 18;

        public static int Damage(OwnedPet o)
        {
            var d = o != null ? GuildData.Pet(o.petId) : null;
            if (d == null) return 1;
            return Mathf.Max(1, Mathf.RoundToInt(d.dmg * o.level * (o.level >= MaxLevel ? 1.3f : 1f)));
        }

        // ------------------------------------------------------------------ loja / janela
        public static bool Buy(string petId)
        {
            var d = GuildData.Pet(petId);
            if (d == null) return false;
            if (Owns(petId)) { GameState.Notify("Você já tem " + d.name + "."); return false; }
            if (GameState.P.coins < d.price) { GameState.Notify("Moedas insuficientes."); return false; }
            GameState.AddCoins(-d.price);
            var o = new OwnedPet { uid = G.nextPetUid++, petId = d.id, name = d.name, level = 1, xp = 0 };
            G.pets.Add(o);
            if (G.activePet < 0) G.activePet = o.uid;
            GameState.Save();
            GuildState.Save();
            GuildState.Emit();
            GameState.Notify("Novo companheiro: " + d.name + "!");
            Sfx.Play("item_rare");
            Refresh();
            Lore.CheckUnlocks(true);     // [Progressão]
            Achievements.Check();
            return true;
        }

        public static void Equip(int uid)
        {
            if (uid >= 0 && Find(uid) == null) return;
            G.activePet = uid;
            GuildState.Save();
            GuildState.Emit();
            Refresh();
            var o = ActiveOwned;
            if (o != null) GameState.Notify(o.name + " está te acompanhando.");
        }

        public static void Rename(int uid, string name)
        {
            var o = Find(uid);
            if (o == null) return;
            name = (name ?? "").Trim();
            if (name.Length == 0) { GameState.Notify("Digite um nome."); return; }
            if (name.Length > 16) name = name.Substring(0, 16);
            o.name = name;
            GuildState.Save();
            GuildState.Emit();
            if (Current != null && Current.owned == o) Current.RefreshLabel();
            GameState.Notify("Agora seu pet se chama " + name + ".");
        }

        // ------------------------------------------------------------------ runtime
        /// <summary>Cria/remove/recria o pet do mundo conforme o pet ativo.</summary>
        public static void Refresh()
        {
            var o = ActiveOwned;
            var pl = Game.I != null ? Game.I.player : null;
            if (o == null || pl == null)
            {
                if (Current != null) Object.Destroy(Current.gameObject);
                Current = null;
                return;
            }
            bool evo = o.level >= MaxLevel;
            if (Current != null && Current.owned == o && Current.evolved == evo) return;
            Vector3 pos = Current != null ? Current.transform.position : pl.transform.position + new Vector3(1f, 0f, -1f);
            if (Current != null) Object.Destroy(Current.gameObject);
            Current = Pet.Create(o, pos);
        }

        /// <summary>Chamado pelo Game depois de posicionar o herói num mapa novo.</summary>
        public static void OnTravel(Player p)
        {
            Refresh();
            if (Current != null && p != null) Current.Place(p.transform.position);
            Achievements.OnTravel();     // [Progressão] andar sem dano, rastreio de notas
            Collectibles.OnTravel();     // [Progressão] colecionáveis da Torre
        }

        public static void OnHeroKill(bool boss)
        {
            var o = ActiveOwned;
            if (o == null || Current == null) return;
            AddXp(o, boss ? 20 : 2);
        }

        public static void OnPetCollected()
        {
            var o = ActiveOwned;
            if (o != null) AddXp(o, 3);
        }

        public static void AddXp(OwnedPet o, int n)
        {
            if (o == null || o.level >= MaxLevel || n <= 0) return;
            var d = GuildData.Pet(o.petId);
            o.xp += n;
            bool up = false;
            while (o.level < MaxLevel && o.xp >= XpToNext(o.level))
            {
                o.xp -= XpToNext(o.level);
                o.level++;
                up = true;
                string msg = o.name + " subiu para o nível " + o.level + "!";
                if (o.level == AttackLevel) msg += " Agora ataca inimigos.";
                if (o.level == SpecialLevel && d != null) msg += " Aprendeu: " + d.special + "!";
                if (o.level == MaxLevel) msg = o.name + " EVOLUIU! Nível máximo.";
                GameState.Notify(msg);
            }
            if (o.level >= MaxLevel) o.xp = 0;
            if (up)
            {
                Sfx.Play("levelup", null, 0.5f);
                if (Current != null && Current.owned == o)
                {
                    FX.Pillar(Current.transform.position, Current.MainColor, o.level >= MaxLevel ? 1.4f : 0.6f);
                    Current.RefreshLabel();
                }
                GuildState.Save();
                Refresh();   // nível 10 recria a forma evoluída
            }
            else GuildState.MarkDirty();
            GuildState.Emit();
        }
    }

    // ======================================================================
    /// <summary>Pet no mundo: corpo procedural fofo feito de primitivas.</summary>
    public class Pet : MonoBehaviour
    {
        public OwnedPet owned;
        public PetDef def;
        public bool evolved;
        public Color MainColor => col;

        Transform model, shadow, wingL, wingR, tail, aura, label;
        readonly List<Transform> wobble = new();
        TextMesh nameText;
        Color col, col2, eyeCol;
        float s, hover, t, atkCd = 1f, specialCd = 3f, fetchClock, sparkClock, pulse, sideSign = 1f, topY;
        bool flyer, moving;
        Loot fetch;
        Vector3 lookDir = Vector3.forward;
        static Font font;
        static readonly List<Enemy> buf = new();

        public static Pet Create(OwnedPet o, Vector3 pos)
        {
            var d = GuildData.Pet(o.petId);
            if (d == null) return null;
            var go = new GameObject("Pet_" + d.id);
            go.transform.position = pos;
            var p = go.AddComponent<Pet>();
            p.owned = o;
            p.def = d;
            p.Build();
            return p;
        }

        public void Place(Vector3 heroPos)
        {
            transform.position = heroPos + new Vector3(1f, 0f, -1f);
            fetch = null;
            FX.Sparkle(transform.position + Vector3.up * 0.5f, col, 0.4f, 0.2f);
        }

        // ------------------------------------------------------------------ construção
        GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c, float smooth = 0.55f, Color? emission = null)
        {
            var g = U.Prim(type, parent, pos, scale, c);
            g.GetComponent<Renderer>().sharedMaterial = U.Lit(c, smooth, emission);
            return g;
        }

        Transform Pivot(Transform parent, string n, Vector3 pos)
        {
            var p = new GameObject(n).transform;
            p.SetParent(parent, false);
            p.localPosition = pos;
            return p;
        }

        void Build()
        {
            evolved = owned.level >= Pets.MaxLevel;
            col = U.Hex(def.color);
            col2 = U.Hex(string.IsNullOrEmpty(def.color2) ? def.color : def.color2);
            eyeCol = U.Hex(string.IsNullOrEmpty(def.eyes) ? "7af0ff" : def.eyes);
            s = def.size * 0.9f * (evolved ? 1.35f : 1f);
            string shape = def.shape ?? "slime";
            flyer = shape == "dragaozinho" || shape == "coruja" || shape == "fantasminha";
            hover = flyer ? 0.95f : 0f;

            model = Pivot(transform, "modelo", Vector3.zero);
            var body = Pivot(model, "corpo", Vector3.zero);
            Color white = new Color(0.97f, 0.97f, 1f);
            Color pink = U.Hex("ff8fb0");
            Color dark = new Color(0.06f, 0.05f, 0.08f);

            switch (shape)
            {
                case "dragaozinho":
                {
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, 0f, 0f), new Vector3(0.6f, 0.54f, 0.58f) * s, col, 0.6f);
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, -0.06f * s, 0.18f * s), new Vector3(0.4f, 0.34f, 0.22f) * s, col2, 0.4f);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var h = Part(PrimitiveType.Capsule, body, new Vector3(k * 0.13f * s, 0.3f * s, -0.02f * s), new Vector3(0.07f, 0.09f, 0.07f) * s, col2, 0.7f);
                        h.transform.localRotation = Quaternion.Euler(-15f, 0f, -k * 25f);
                        Part(PrimitiveType.Sphere, body, new Vector3(k * 0.12f * s, -0.27f * s, 0.06f * s), Vector3.one * 0.13f * s, col, 0.5f);
                    }
                    wingL = Pivot(body, "asaE", new Vector3(-0.25f * s, 0.08f * s, -0.1f * s));
                    wingR = Pivot(body, "asaD", new Vector3(0.25f * s, 0.08f * s, -0.1f * s));
                    Part(PrimitiveType.Cube, wingL, new Vector3(-0.18f * s, 0f, 0f), new Vector3(0.36f, 0.03f, 0.24f) * s, col2, 0.5f);
                    Part(PrimitiveType.Cube, wingR, new Vector3(0.18f * s, 0f, 0f), new Vector3(0.36f, 0.03f, 0.24f) * s, col2, 0.5f);
                    tail = Pivot(body, "cauda", new Vector3(0f, -0.12f * s, -0.26f * s));
                    var tc = Part(PrimitiveType.Capsule, tail, new Vector3(0f, 0f, -0.08f * s), new Vector3(0.09f, 0.14f, 0.09f) * s, col, 0.5f);
                    tc.transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
                    var tip = Part(PrimitiveType.Cube, tail, new Vector3(0f, 0.04f * s, -0.22f * s), new Vector3(0.1f, 0.1f, 0.03f) * s, col2, 0.5f);
                    tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    Face(body, 0.07f * s, 0.24f * s, 0.12f * s, 0.18f * s, pink, dark, white);
                    topY = 0.38f * s;
                    break;
                }
                case "coruja":
                {
                    Part(PrimitiveType.Sphere, body, Vector3.zero, new Vector3(0.58f, 0.62f, 0.54f) * s, col, 0.4f);
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, -0.08f * s, 0.17f * s), new Vector3(0.42f, 0.42f, 0.22f) * s, col2, 0.3f);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var e = Part(PrimitiveType.Cube, body, new Vector3(k * 0.17f * s, 0.31f * s, 0f), new Vector3(0.08f, 0.18f, 0.06f) * s, col, 0.4f);
                        e.transform.localRotation = Quaternion.Euler(0f, 0f, -k * 25f);
                        Part(PrimitiveType.Sphere, body, new Vector3(k * 0.08f * s, -0.31f * s, 0.08f * s), new Vector3(0.1f, 0.06f, 0.12f) * s, U.Hex("ffa53d"), 0.4f);
                        // disco claro em volta do olho
                        Part(PrimitiveType.Sphere, body, new Vector3(k * 0.12f * s, 0.07f * s, 0.2f * s), new Vector3(0.24f, 0.24f, 0.08f) * s, Color.Lerp(col2, white, 0.5f), 0.3f);
                    }
                    var beak = Part(PrimitiveType.Cube, body, new Vector3(0f, -0.04f * s, 0.29f * s), new Vector3(0.07f, 0.07f, 0.06f) * s, U.Hex("ffa53d"), 0.5f);
                    beak.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    wingL = Pivot(body, "asaE", new Vector3(-0.27f * s, 0.02f * s, -0.02f * s));
                    wingR = Pivot(body, "asaD", new Vector3(0.27f * s, 0.02f * s, -0.02f * s));
                    Part(PrimitiveType.Sphere, wingL, new Vector3(-0.04f * s, -0.08f * s, 0f), new Vector3(0.1f, 0.3f, 0.22f) * s, col, 0.4f);
                    Part(PrimitiveType.Sphere, wingR, new Vector3(0.04f * s, -0.08f * s, 0f), new Vector3(0.1f, 0.3f, 0.22f) * s, col, 0.4f);
                    Face(body, 0.07f * s, 0.24f * s, 0.12f * s, 0.21f * s, pink, dark, white);
                    topY = 0.42f * s;
                    break;
                }
                case "gatinho":
                {
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.27f * s, 0f), new Vector3(0.58f, 0.5f, 0.56f) * s, col, 0.4f);
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.2f * s, 0.18f * s), new Vector3(0.36f, 0.3f, 0.22f) * s, col2, 0.3f);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var ear = Part(PrimitiveType.Cube, body, new Vector3(k * 0.15f * s, 0.49f * s, 0f), new Vector3(0.16f, 0.16f, 0.05f) * s, col, 0.4f);
                        ear.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                        Part(PrimitiveType.Cube, ear.transform, new Vector3(0f, 0f, 0.4f), new Vector3(0.55f, 0.55f, 1f), pink, 0.3f);
                        Part(PrimitiveType.Sphere, body, new Vector3(k * 0.13f * s, 0.04f * s, 0.12f * s), Vector3.one * 0.12f * s, col2, 0.3f);
                        Part(PrimitiveType.Sphere, body, new Vector3(k * 0.13f * s, 0.04f * s, -0.13f * s), Vector3.one * 0.12f * s, col2, 0.3f);
                    }
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.25f * s, 0.29f * s), new Vector3(0.06f, 0.045f, 0.04f) * s, pink, 0.6f);
                    tail = Pivot(body, "cauda", new Vector3(0f, 0.18f * s, -0.25f * s));
                    var tc = Part(PrimitiveType.Capsule, tail, new Vector3(0f, 0.16f * s, -0.04f * s), new Vector3(0.07f, 0.18f, 0.07f) * s, col, 0.4f);
                    tc.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                    Part(PrimitiveType.Sphere, tail, new Vector3(0f, 0.34f * s, -0.08f * s), Vector3.one * 0.09f * s, col2, 0.4f);
                    Face(body, 0.31f * s, 0.24f * s, 0.12f * s, 0.17f * s, pink, dark, white);
                    topY = 0.6f * s;
                    break;
                }
                case "cogumelo":
                {
                    Color cream = U.Hex("f3e6cf");
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.22f * s, 0f), new Vector3(0.46f, 0.44f, 0.44f) * s, cream, 0.3f);
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.36f * s, 0f), new Vector3(0.7f, 0.08f, 0.7f) * s, col2, 0.3f);
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.47f * s, 0f), new Vector3(0.8f, 0.42f, 0.8f) * s, col, 0.65f);
                    Vector3[] spots =
                    {
                        new Vector3(0.18f, 0.6f, 0.13f), new Vector3(-0.17f, 0.61f, 0.06f), new Vector3(0.03f, 0.66f, -0.14f),
                        new Vector3(-0.06f, 0.57f, 0.25f), new Vector3(0.27f, 0.52f, -0.12f), new Vector3(-0.26f, 0.53f, -0.15f)
                    };
                    foreach (var sp in spots) Part(PrimitiveType.Sphere, body, sp * s, new Vector3(0.11f, 0.06f, 0.11f) * s, white, 0.4f);
                    for (int k = -1; k <= 1; k += 2)
                        Part(PrimitiveType.Sphere, body, new Vector3(k * 0.1f * s, 0.03f * s, 0.05f * s), new Vector3(0.13f, 0.08f, 0.15f) * s, cream, 0.3f);
                    Face(body, 0.21f * s, 0.18f * s, 0.1f * s, 0.15f * s, pink, dark, white);
                    topY = 0.68f * s;
                    break;
                }
                case "fantasminha":
                {
                    Part(PrimitiveType.Sphere, body, Vector3.zero, new Vector3(0.56f, 0.64f, 0.54f) * s, col, 0.7f, col * 0.35f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = (i + 0.5f) / 4f * Mathf.PI * 2f;
                        var w = Part(PrimitiveType.Sphere, body, new Vector3(Mathf.Sin(a) * 0.15f * s, -0.29f * s, Mathf.Cos(a) * 0.14f * s), Vector3.one * 0.19f * s, col, 0.7f, col * 0.35f);
                        wobble.Add(w.transform);
                    }
                    for (int k = -1; k <= 1; k += 2)
                        Part(PrimitiveType.Sphere, body, new Vector3(k * 0.29f * s, -0.04f * s, 0.05f * s), new Vector3(0.14f, 0.1f, 0.1f) * s, col, 0.7f, col * 0.35f);
                    Face(body, 0.08f * s, 0.24f * s, 0.12f * s, 0.17f * s, pink, dark, white);
                    var lg = new GameObject("luz_fantasma");
                    lg.transform.SetParent(model, false);
                    var lc = lg.AddComponent<Light>();
                    lc.type = LightType.Point; lc.color = col; lc.intensity = 0.8f; lc.range = 2.5f; lc.shadows = LightShadows.None;
                    topY = 0.36f * s;
                    break;
                }
                default: // slime
                {
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.24f * s, 0f), new Vector3(0.74f, 0.5f, 0.7f) * s, col, 0.95f, col * 0.15f);
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.48f * s, -0.02f * s), Vector3.one * 0.2f * s, col, 0.95f, col * 0.15f);
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.58f * s, -0.03f * s), new Vector3(0.1f, 0.16f, 0.1f) * s, col, 0.95f, col * 0.15f);
                    Part(PrimitiveType.Sphere, body, new Vector3(0.18f * s, 0.38f * s, 0.12f * s), new Vector3(0.1f, 0.06f, 0.06f) * s, white, 1f, white * 0.6f);
                    Face(body, 0.27f * s, 0.27f * s, 0.13f * s, 0.19f * s, pink, dark, white);
                    topY = 0.64f * s;
                    break;
                }
            }

            if (evolved)
            {
                Color gold = U.Hex("ffd04a");
                var crown = Pivot(body, "coroa", new Vector3(0f, topY, 0f));
                Part(PrimitiveType.Cylinder, crown, Vector3.zero, new Vector3(0.24f, 0.03f, 0.24f) * s, gold, 0.9f, gold * 0.6f);
                for (int i = 0; i < 4; i++)
                {
                    float a = i / 4f * Mathf.PI * 2f;
                    var sp = Part(PrimitiveType.Cube, crown, new Vector3(Mathf.Sin(a) * 0.1f * s, 0.06f * s, Mathf.Cos(a) * 0.1f * s), Vector3.one * 0.06f * s, gold, 0.9f, gold * 0.6f);
                    sp.transform.localRotation = Quaternion.Euler(45f, a * Mathf.Rad2Deg, 45f);
                }
                var ring = FX.FlatQuad("aura", transform.position, U.RingTexture(), new Color(col.r, col.g, col.b, 0.85f), true);
                ring.transform.SetParent(transform, false);
                ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localScale = Vector3.one * 1.5f * s;
                aura = ring.transform;
                var lg = new GameObject("luz_aura");
                lg.transform.SetParent(transform, false);
                lg.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                var lc = lg.AddComponent<Light>();
                lc.type = LightType.Point; lc.color = col; lc.intensity = 1.3f; lc.range = 3.5f; lc.shadows = LightShadows.None;
            }

            // sombra
            var sh = FX.FlatQuad("sombra", transform.position, U.SoftTexture(), new Color(0f, 0f, 0f, 0.5f), false);
            sh.transform.SetParent(transform, false);
            sh.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            sh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sh.transform.localScale = Vector3.one * 0.8f * s;
            shadow = sh.transform;

            // nome
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var tg = new GameObject("nome");
            tg.transform.SetParent(transform, false);
            tg.transform.localPosition = new Vector3(0f, hover + topY + 0.38f, 0f);
            nameText = tg.AddComponent<TextMesh>();
            nameText.font = font;
            nameText.fontSize = 48;
            nameText.characterSize = 0.03f;
            nameText.anchor = TextAnchor.MiddleCenter;
            nameText.alignment = TextAlignment.Center;
            var mr = tg.GetComponent<MeshRenderer>();
            if (font != null) mr.sharedMaterial = font.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            label = tg.transform;
            RefreshLabel();

            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.receiveShadows = false;
        }

        /// <summary>Olhos grandes e brilhantes, bochechas rosadas e boquinha.</summary>
        void Face(Transform parent, float y, float z, float spread, float size, Color pink, Color dark, Color white)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                float x = k * spread;
                Part(PrimitiveType.Sphere, parent, new Vector3(x, y, z), new Vector3(size, size * 1.1f, size * 0.7f), white, 0.8f);
                Part(PrimitiveType.Sphere, parent, new Vector3(x, y - size * 0.04f, z + size * 0.2f), new Vector3(size * 0.72f, size * 0.8f, size * 0.4f), eyeCol, 0.95f, eyeCol * 1.6f);
                Part(PrimitiveType.Sphere, parent, new Vector3(x, y - size * 0.04f, z + size * 0.3f), new Vector3(size * 0.38f, size * 0.45f, size * 0.25f), dark, 0.95f);
                Part(PrimitiveType.Sphere, parent, new Vector3(x + size * 0.13f, y + size * 0.18f, z + size * 0.38f), Vector3.one * size * 0.2f, white, 1f, white * 2f);
                Part(PrimitiveType.Sphere, parent, new Vector3(k * (spread + size * 0.62f), y - size * 0.6f, z - size * 0.15f), new Vector3(size * 0.38f, size * 0.2f, size * 0.12f), pink, 0.3f, pink * 0.25f);
            }
            Part(PrimitiveType.Sphere, parent, new Vector3(0f, y - size * 0.62f, z + size * 0.05f), new Vector3(size * 0.2f, size * 0.12f, size * 0.1f), dark, 0.5f);
        }

        public void RefreshLabel()
        {
            if (nameText == null || owned == null) return;
            nameText.text = owned.name + "  Nv " + owned.level;
            nameText.color = evolved ? U.Hex("ffd76a") : new Color(1f, 1f, 1f, 0.9f);
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            var g = Game.I;
            var pl = g != null ? g.player : null;
            if (pl == null) return;
            float dt = Time.deltaTime;
            t += dt;

            Vector3 hp = pl.transform.position;
            Vector3 pos = transform.position;
            if (g.Traveling) { transform.position = hp + new Vector3(1f, 0f, -1f); fetch = null; return; }
            if (U.Flat(hp - pos).magnitude > 16f) { Place(hp); return; }

            // buscar loot
            fetchClock -= dt;
            if (fetch != null && !fetch.Ready) fetch = null;
            if (fetch == null && fetchClock <= 0f)
            {
                fetchClock = 0.3f;
                fetch = Loot.NearestFor(pos, hp, def.radius);
            }

            Vector3 goal;
            if (fetch != null) goal = fetch.transform.position;
            else
            {
                Vector3 fwd = U.Flat(pl.facing);
                fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                goal = hp - fwd * 1.2f + right * 1.0f * sideSign;
            }
            Vector3 to = U.Flat(goal - pos);
            float d = to.magnitude;
            float sp = def.speed * (fetch != null ? 1.4f : (d > 4f ? 1.7f : 1f)) * (evolved ? 1.15f : 1f);
            moving = false;
            float stop = fetch != null ? 0.05f : 0.4f;
            if (d > stop)
            {
                float step = Mathf.Min(d, sp * dt);
                pos += to / d * step;
                moving = d > 0.3f;
            }
            pos.y = Mathf.Lerp(pos.y, hp.y, 1f - Mathf.Exp(-10f * dt));
            transform.position = pos;

            if (fetch != null && U.Flat(fetch.transform.position - pos).magnitude < 0.6f)
            {
                fetch.Collect(true);
                fetch = null;
                pulse = 1f;
                if (Random.value < 0.3f) sideSign = -sideSign;
            }

            // combate
            Enemy focus = null;
            bool fight = owned.level >= Pets.AttackLevel && g.InDungeon && pl.canFight && pl.state != "dead";
            if (fight)
            {
                atkCd -= dt;
                specialCd -= dt;
                if (atkCd <= 0f)
                {
                    var e = NearestEnemy(pos, hp, 7.5f);
                    if (e != null) { Shoot(e); focus = e; atkCd = Mathf.Max(0.55f, 1.5f - owned.level * 0.07f); }
                    else atkCd = 0.25f;
                }
                if (owned.level >= Pets.SpecialLevel && specialCd <= 0f)
                {
                    var e = NearestEnemy(pos, hp, 5.5f);
                    if (e != null) { Special(e); focus = e; specialCd = evolved ? 7f : 9f; }
                    else specialCd = 0.5f;
                }
            }

            // olhar
            Vector3 look = moving ? to : (focus != null ? U.Flat(focus.transform.position - pos) : U.Flat(hp - pos));
            if (look.sqrMagnitude > 0.01f) lookDir = look.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir, Vector3.up), 1f - Mathf.Exp(-8f * dt));

            Animate(dt);

            sparkClock -= dt;
            if (sparkClock <= 0f)
            {
                sparkClock = evolved ? 0.35f : 0.9f;
                FX.Sparkle(model.position + Vector3.up * 0.2f * s, evolved ? Color.Lerp(col, U.Hex("ffd76a"), 0.5f) : col, evolved ? 0.35f : 0.2f, 0.1f);
            }
        }

        void Animate(float dt)
        {
            pulse = Mathf.Max(0f, pulse - dt * 4f);
            float y = hover;
            Vector3 sc = Vector3.one;
            string shape = def.shape ?? "slime";
            if (flyer)
            {
                y += Mathf.Sin(t * (shape == "fantasminha" ? 1.8f : 2.6f)) * (shape == "fantasminha" ? 0.15f : 0.1f);
                float flap = Mathf.Sin(t * (moving ? 22f : 12f));
                float amp = shape == "coruja" ? 25f : 35f;
                if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, -10f - flap * amp);
                if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, 10f + flap * amp);
                if (shape == "fantasminha")
                {
                    model.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.5f) * 6f);
                    for (int i = 0; i < wobble.Count; i++)
                        wobble[i].localScale = Vector3.one * 0.19f * s * (1f + 0.15f * Mathf.Sin(t * 5f + i * 1.6f));
                }
                else model.localRotation = Quaternion.Euler(moving ? 12f : 0f, 0f, 0f);
            }
            else
            {
                float hop = moving ? Mathf.Abs(Mathf.Sin(t * 10f)) : 0f;
                float squash = shape == "slime" ? 0.2f : 0.08f;
                if (moving)
                {
                    y += hop * 0.18f * s * (shape == "slime" ? 1.3f : 1f);
                    float k = 1f - hop;   // no chão = achata
                    sc = new Vector3(1f + squash * k * 0.7f, 1f - squash * k, 1f + squash * k * 0.7f);
                }
                else
                {
                    float b = Mathf.Sin(t * 3f) * 0.035f;
                    sc = new Vector3(1f - b * 0.5f, 1f + b, 1f - b * 0.5f);
                }
            }
            if (pulse > 0f) sc *= 1f + 0.15f * Mathf.Sin(pulse * Mathf.PI);
            model.localPosition = new Vector3(0f, y, 0f);
            model.localScale = sc;
            if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * (moving ? 12f : 4f)) * 25f, 0f);
            if (shadow != null) shadow.localScale = Vector3.one * 0.8f * s * (flyer ? 0.75f - 0.08f * Mathf.Sin(t * 2.6f) : 1f);
            if (aura != null) aura.Rotate(0f, 0f, 60f * dt, Space.Self);
        }

        void LateUpdate()
        {
            var cam = CameraRig.Cam;
            if (cam != null && label != null) label.rotation = cam.transform.rotation;
        }

        // ------------------------------------------------------------------ combate
        static Enemy NearestEnemy(Vector3 from, Vector3 hero, float range)
        {
            var g = Game.I;
            if (g == null) return null;
            Enemy best = null;
            float bd = range;
            foreach (var e in g.enemies)
            {
                if (e == null || e.dead) continue;
                Vector3 ep = e.transform.position;
                if (U.Flat(ep - hero).magnitude > 12f) continue;
                float d = U.Flat(ep - from).magnitude;
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        static void CollectAround(Vector3 center, float radius)
        {
            buf.Clear();
            var g = Game.I;
            if (g == null) return;
            foreach (var e in g.enemies)
                if (e != null && !e.dead && U.Flat(e.transform.position - center).magnitude <= radius) buf.Add(e);
        }

        Vector3 Mouth => transform.position + Vector3.up * (hover + 0.25f * s) + transform.forward * 0.25f * s;

        void Shoot(Enemy e)
        {
            Color c = Color.Lerp(col, eyeCol, 0.5f);
            PetShot.Spawn(Mouth, e, Pets.Damage(owned), c);
            Sfx.Play("magic_cast", Mouth, 0.3f, 0.25f);
            pulse = 1f;
        }

        void Special(Enemy target)
        {
            int dmg = Pets.Damage(owned) * 2;
            Vector3 p = transform.position;
            Vector3 dir = U.Flat(target.transform.position - p);
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
            HUD.Popup(p + Vector3.up * (hover + topY + 0.6f), def.special ?? "Especial!", col, false);
            pulse = 1f;
            switch (def.shape)
            {
                case "dragaozinho":   // Sopro de Fogo: cone de 4,5 m
                {
                    Color fire = U.Hex("ff7a2a");
                    for (int i = 1; i <= 5; i++)
                        FX.Burst(Mouth + dir * (i * 0.8f), Color.Lerp(U.Hex("ffd04a"), fire, i / 5f), 0.35f + 0.08f * i, 10);
                    FX.Flash(Mouth + dir * 2f, fire, 3f, 0.3f);
                    Sfx.Play("fireball", p);
                    CollectAround(p, 4.5f);
                    foreach (var e in buf)
                    {
                        Vector3 de = U.Flat(e.transform.position - p);
                        if (de.sqrMagnitude < 0.01f || Vector3.Angle(dir, de) <= 40f) e.TakeHit(dmg, p, false);
                    }
                    break;
                }
                case "coruja":        // Pio Sônico: atordoa em volta
                {
                    FX.Ring(p, 4f, col, 0.5f);
                    FX.Ring(p, 3f, Color.white, 0.4f);
                    Sfx.Play("lightning", p, 0.7f);
                    CollectAround(p, 4f);
                    foreach (var e in buf) { e.TakeHit(Mathf.Max(1, dmg / 2), p, false); if (!e.dead) e.Stun(0.9f); }
                    break;
                }
                case "gatinho":       // Arranhão Triplo
                    StartCoroutine(Scratch(target, dmg));
                    break;
                case "cogumelo":      // Esporos Curativos
                {
                    var pl = Game.I != null ? Game.I.player : null;
                    if (pl != null)
                    {
                        float heal = GameState.maxHp * 0.12f;
                        GameState.P.hp = Mathf.Min(GameState.maxHp, GameState.P.hp + heal);
                        GameState.Emit();
                        HUD.Popup(pl.transform.position + Vector3.up * 2.4f, "+" + Mathf.RoundToInt(heal), U.Hex("8fe08a"), false);
                        FX.Pillar(pl.transform.position, U.Hex("8fe08a"), 0.7f);
                        Sfx.Play("heal", pl.transform.position, 0.7f);
                    }
                    FX.Dust(p, col, 1.2f);
                    CollectAround(p, 3f);
                    foreach (var e in buf) e.TakeHit(Mathf.Max(1, dmg / 2), p, false);
                    break;
                }
                case "fantasminha":   // Assombro: atordoa o grupo do alvo
                {
                    Vector3 c = target.transform.position;
                    FX.Burst(c + Vector3.up, U.Hex("b98aff"), 1f, 30);
                    FX.Ring(c, 4.5f, U.Hex("b98aff"), 0.5f);
                    Sfx.Play("magic_cast", c, 0.8f);
                    CollectAround(c, 4.5f);
                    foreach (var e in buf) { e.TakeHit(Mathf.Max(1, dmg / 2), p, false); if (!e.dead) e.Stun(1.6f); }
                    break;
                }
                default:              // slime: Gosma Grudenta (gruda = atordoa curto)
                {
                    Vector3 c = target.transform.position;
                    FX.Burst(c + Vector3.up * 0.6f, col, 0.8f, 20);
                    FX.Ring(c, 1.8f, col, 0.45f);
                    Sfx.Play("hit", c, 0.7f);
                    target.TakeHit(dmg, p, false);
                    CollectAround(c, 1.8f);
                    foreach (var e in buf) if (!e.dead) e.Stun(e == target ? 1.4f : 0.8f);
                    break;
                }
            }
            buf.Clear();
        }

        IEnumerator Scratch(Enemy target, int dmg)
        {
            for (int i = 0; i < 3; i++)
            {
                if (target == null || target.dead) yield break;
                Vector3 tp = target.transform.position;
                Vector3 from = transform.position;
                Vector3 dir = U.Flat(tp - from);
                dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
                transform.position = tp - dir * 0.7f;
                FX.Slash(tp + Vector3.up * 0.7f, Quaternion.Euler(0f, (i - 1) * 35f, 0f) * dir, Color.white, 0.9f);
                Sfx.Play("swing", tp, 0.6f);
                target.TakeHit(Mathf.Max(1, dmg / 2), from, i == 2);
                yield return new WaitForSeconds(0.14f);
            }
        }

        void OnDestroy()
        {
            if (Pets.Current == this) Pets.Current = null;
        }
    }

    // ======================================================================
    /// <summary>Projétil pequeno e teleguiado do pet.</summary>
    public class PetShot : MonoBehaviour
    {
        Enemy target;
        int dmg;
        Color c;
        Vector3 dir, origin;
        float life;

        public static void Spawn(Vector3 pos, Enemy target, int dmg, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "tiro_pet";
            go.transform.SetParent(FX.Root, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.2f;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = U.Lit(c, 0.9f, c * 2.5f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = U.Fx(true, U.WhiteTexture());
            tr.time = 0.18f;
            tr.widthMultiplier = 0.14f;
            tr.minVertexDistance = 0.05f;
            tr.startColor = c;
            tr.endColor = new Color(c.r, c.g, c.b, 0f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            var s = go.AddComponent<PetShot>();
            s.target = target; s.dmg = dmg; s.c = c; s.origin = pos;
            s.dir = target != null ? (target.transform.position + Vector3.up - pos).normalized : Vector3.forward;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            if (life > 2.5f) { Destroy(gameObject); return; }
            Vector3 pos = transform.position;
            bool alive = target != null && !target.dead;
            if (alive)
            {
                Vector3 aim = target.transform.position + Vector3.up * 1f;
                Vector3 to = aim - pos;
                if (to.magnitude < 0.5f)
                {
                    target.TakeHit(dmg, origin, Random.value < 0.1f);
                    FX.Burst(pos, c, 0.4f, 8);
                    Destroy(gameObject);
                    return;
                }
                dir = to.normalized;
            }
            else if (life > 0.6f)
            {
                FX.Burst(pos, c, 0.3f, 6);
                Destroy(gameObject);
                return;
            }
            transform.position = pos + dir * 14f * dt;
        }
    }
}
