using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Drakantus.EditorTools
{
    /// <summary>
    /// Drakantus > Criador de Andares. Aba "Gerar": escolhe tamanho, bioma, estrutura e conteúdo e gera o
    /// andar inteiro numa cena Assets/Floors/Andar_XX.unity. Aba "Retoque": paleta para clicar na Scene View
    /// e colocar peças. Aba "Andares": lista do FloorRegistry.
    /// </summary>
    public class FloorCreatorWindow : EditorWindow
    {
        const string PrefsKey = "Drakantus.FloorCreator.Settings";

        FloorSettings s;
        Vector2 scroll;
        int mainTab;
        bool fAndar = true, fTam = true, fBioma = true, fEstr = true, fCont = true, fGerar = true;

        string[] biomeIds = new string[0], biomeNames = new string[0];
        string[] bossIds = new string[0], bossNames = new string[0];
        string[] classIds = new string[0], classNames = new string[0];

        // paleta
        int palTab;
        string armed, armedLabel;
        float armedYaw;
        bool palElite, palSnap = true;
        bool[] palAbil = new bool[EnemyAbilities.AllIds.Length];
        string palBiome = "";
        static readonly string[] PalTabs = { "Chão/Paredes", "Decoração", "Luzes", "Armadilhas", "Portas & Alavancas", "Fumaça & Efeitos", "Inimigos", "Mini-chefes", "Chefes", "Baús", "Escadas", "Santuário/Portais" };

        GUIStyle titleStyle, headStyle, bigBtn, wrapLabel;
        readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

        [MenuItem("Drakantus/Criador de Andares", false, 20)]
        public static void Open()
        {
            var w = GetWindow<FloorCreatorWindow>("Criador de Andares");
            w.minSize = new Vector2(380f, 520f);
            w.Show();
        }

        // ------------------------------------------------------------------ ciclo
        void OnEnable()
        {
            LoadPrefs();
            RefreshLists();
            SceneView.duringSceneGui += OnScene;
        }

        void OnDisable()
        {
            SavePrefs();
            SceneView.duringSceneGui -= OnScene;
        }

        void LoadPrefs()
        {
            s = null;
            string json = EditorPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { s = JsonUtility.FromJson<FloorSettings>(json); } catch { s = null; }
            }
            if (s == null) { s = new FloorSettings(); s.ApplyPreset(); }
        }

        void SavePrefs()
        {
            if (s != null) EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(s));
        }

        void RefreshLists()
        {
            Biomes.Reload();
            var bl = Biomes.All();
            biomeIds = new string[bl.Count];
            biomeNames = new string[bl.Count];
            for (int i = 0; i < bl.Count; i++) { biomeIds[i] = bl[i].id; biomeNames[i] = (string.IsNullOrEmpty(bl[i].category) ? "" : bl[i].category + "/") + bl[i].name; }

            GameData.Load();
            var ids = new List<string> { "", "-" };
            var names = new List<string> { "Aleatório (do bioma)", "Sem chefe" };
            foreach (var d in GameData.Enemies.Values)
                if (d.boss && !d.id.StartsWith("mb_")) { ids.Add(d.id); names.Add(d.name + "  (" + d.id + ")"); }
            bossIds = ids.ToArray();
            bossNames = names.ToArray();

            var ci = new List<string>(); var cn = new List<string>();
            foreach (var c in GameData.ClassOrder) { ci.Add(c.id); cn.Add(c.name); }
            classIds = ci.ToArray();
            classNames = cn.ToArray();
        }

        void Styles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            headStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12 };
            bigBtn = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 38f };
            wrapLabel = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
        }

        Texture2D Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (icons.TryGetValue(name, out var t)) return t;
            t = EditorGUIUtility.FindTexture(name);
            icons[name] = t;
            return t;
        }

        GUIContent C(string text, string icon = null, string tip = null) => new GUIContent(" " + text, Icon(icon), tip ?? "");

        bool Section(ref bool open, string title, string icon, Color color)
        {
            EditorGUILayout.Space(4f);
            var r = GUILayoutUtility.GetRect(10f, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(color.r, color.g, color.b, 0.22f));
            EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height), color);
            var fr = new Rect(r.x + 8f, r.y + 2f, r.width - 8f, r.height - 2f);
            open = EditorGUI.Foldout(fr, open, C(title, icon), true, headStyle);
            return open;
        }

        void Help(string text) => EditorGUILayout.LabelField(text, wrapLabel);

        // ------------------------------------------------------------------ GUI
        void OnGUI()
        {
            Styles();
            if (s == null) LoadPrefs();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Criador de Andares da Torre", titleStyle);
            Help("Escolha as opções e aperte GERAR. Depois retoque à mão (aba Retoque) e use Salvar/Testar.");
            mainTab = GUILayout.Toolbar(mainTab, new[] { C("Gerar", "SceneAsset Icon"), C("Retoque", "Prefab Icon"), C("Andares", "Folder Icon") }, GUILayout.Height(26f));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            switch (mainTab)
            {
                case 0: GenerateTab(); break;
                case 1: PaletteTab(); break;
                case 2: FloorsTab(); break;
            }
            if (EditorGUI.EndChangeCheck()) SavePrefs();
            EditorGUILayout.EndScrollView();
        }

        // ================================================================== aba Gerar
        void GenerateTab()
        {
            if (Section(ref fAndar, "Andar", "SceneAsset Icon", new Color(1f, 0.8f, 0.3f)))
            {
                s.floor = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Número do andar", "Ex.: 3 = Andar 03 (cena Assets/Floors/Andar_03)"), s.floor), 1, 99);
                s.id = EditorGUILayout.TextField(new GUIContent("Id (opcional)", "Vazio = tower_f<número>"), s.id);
                EditorGUILayout.LabelField(" ", "Id final: " + s.FloorId + "   ·   cena: " + s.SceneName, EditorStyles.miniLabel);
                if (s.floor <= 2 && string.IsNullOrEmpty(s.id))
                    EditorGUILayout.HelpBox("Andares 1 e 2 já existem (feitos à mão). Salvar com este id SUBSTITUI o andar original no jogo.", MessageType.Warning);
                s.floorName = EditorGUILayout.TextField("Nome", s.floorName);
                s.subtitle = EditorGUILayout.TextField(new GUIContent("Subtítulo", "Vazio = \"Andar 03 · Nome\""), s.subtitle);
                s.difficulty = EditorGUILayout.IntSlider(new GUIContent("Dificuldade", "Vida/dano dos inimigos e armadilhas"), s.difficulty, 1, 10);
                s.recLevel = Mathf.Clamp(EditorGUILayout.IntField("Nível recomendado", s.recLevel), 1, 99);
                s.next = EditorGUILayout.TextField(new GUIContent("Próximo andar (id)", "Vazio = o andar de número seguinte, se existir"), s.next);
            }

            if (Section(ref fTam, "Tamanho", "Terrain Icon", new Color(0.4f, 0.8f, 1f)))
            {
                int np = EditorGUILayout.Popup("Tamanho", s.sizePreset, FloorSettings.SizeNames);
                if (np != s.sizePreset) { s.sizePreset = np; s.ApplyPreset(); }
                if (s.sizePreset == 4)
                {
                    s.width = EditorGUILayout.IntSlider("Largura (m)", s.width, 48, 240);
                    s.length = EditorGUILayout.IntSlider("Comprimento (m)", s.length, 64, 300);
                }
                s.rooms = EditorGUILayout.IntSlider("Número de salas", s.rooms, 3, 30);
                Help("≈ " + s.width + " x " + s.length + " m. Salas demais para o tamanho são cortadas (o relatório avisa).");
            }

            if (Section(ref fBioma, "Bioma", "Light Icon", new Color(0.5f, 1f, 0.5f)))
            {
                if (biomeIds.Length == 0)
                {
                    EditorGUILayout.HelpBox("Resources/Data/biomes.json não encontrado.", MessageType.Error);
                }
                else
                {
                    int bi = Mathf.Max(0, System.Array.IndexOf(biomeIds, s.biome));
                    bi = EditorGUILayout.Popup("Bioma principal", bi, biomeNames);
                    s.biome = biomeIds[bi];
                    var b = Biomes.Get(s.biome);
                    if (b != null && !string.IsNullOrEmpty(b.desc)) Help(b.desc);
                    var names2 = new List<string> { "(nenhum)" }; names2.AddRange(biomeNames);
                    int b2 = string.IsNullOrEmpty(s.biome2) ? 0 : System.Array.IndexOf(biomeIds, s.biome2) + 1;
                    b2 = EditorGUILayout.Popup("Bioma secundário (mistura)", Mathf.Max(0, b2), names2.ToArray());
                    s.biome2 = b2 <= 0 ? "" : biomeIds[b2 - 1];
                    if (!string.IsNullOrEmpty(s.biome2)) s.mix = EditorGUILayout.Slider("Mistura", s.mix, 0.1f, 0.9f);
                }
            }

            if (Section(ref fEstr, "Estrutura", "BoxCollider Icon", new Color(0.8f, 0.6f, 1f)))
            {
                s.levels = EditorGUILayout.IntSlider(new GUIContent("Níveis (escadas)", "0 = plano; 1–3 = salas em alturas diferentes ligadas por rampas, plataformas com escadas"), s.levels, 0, 3);
                s.wideCorridors = EditorGUILayout.Popup("Corredores", s.wideCorridors ? 1 : 0, new[] { "Estreitos (4 m)", "Largos (8 m)" }) == 1;
                s.secretRooms = EditorGUILayout.Toggle(new GUIContent("Sala secreta", "Parede rachada (tecla E) com baú raro"), s.secretRooms);
                s.lockedDoor = EditorGUILayout.Toggle(new GUIContent("Porta trancada + alavanca", "Uma porta no caminho só abre com uma alavanca numa sala anterior"), s.lockedDoor);
                s.arenaDoors = EditorGUILayout.Toggle(new GUIContent("Arenas (portas se fecham)", "Salas de chefe/mini-chefe trancam até todos morrerem"), s.arenaDoors);
            }

            if (Section(ref fCont, "Conteúdo", "Prefab Icon", new Color(1f, 0.45f, 0.4f)))
            {
                s.enemyDensity = EditorGUILayout.Slider("Densidade de inimigos", s.enemyDensity, 0f, 2f);
                s.elitePercent = EditorGUILayout.IntSlider(new GUIContent("% de elites", "Elites: maiores, mais vida, 1–2 habilidades"), s.elitePercent, 0, 100);
                s.miniBosses = EditorGUILayout.IntSlider("Mini-chefes", s.miniBosses, 0, 4);
                int bi = Mathf.Max(0, System.Array.IndexOf(bossIds, s.boss ?? ""));
                bi = EditorGUILayout.Popup("Chefe final", bi, bossNames);
                s.boss = bossIds.Length > 0 ? bossIds[bi] : "";
                s.hiddenTraps = EditorGUILayout.IntSlider("Armadilhas escondidas", s.hiddenTraps, 0, 20);
                s.visibleTraps = EditorGUILayout.IntSlider(new GUIContent("Armadilhas à vista", "Espetos em ciclo, jatos de fogo e placas com flechas"), s.visibleTraps, 0, 20);
                s.poisonFog = EditorGUILayout.Toggle("Névoa venenosa", s.poisonFog);
                s.commonChests = EditorGUILayout.IntSlider("Baús comuns", s.commonChests, 0, 10);
                s.rareChests = EditorGUILayout.IntSlider("Baús raros", s.rareChests, 0, 5);
                s.sanctuary = EditorGUILayout.Toggle(new GUIContent("Santuário no fim", "Cura e conclui o andar (depois da arena do chefe)"), s.sanctuary);
            }

            if (Section(ref fGerar, "Gerar", "Refresh", new Color(0.3f, 1f, 0.6f)))
            {
                EditorGUILayout.BeginHorizontal();
                s.seed = EditorGUILayout.IntField("Semente", s.seed);
                if (GUILayout.Button("Sortear", GUILayout.Width(70f))) s.seed = Random.Range(1, 999999);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(4f);

                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.45f, 1f, 0.5f);
                if (GUILayout.Button(C("GERAR ANDAR COMPLETO", "SceneAsset Icon"), bigBtn)) DoGenerate(false);
                GUI.backgroundColor = old;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(C("Regerar (outra semente)", "Refresh"), GUILayout.Height(26f))) DoGenerate(true);
                if (GUILayout.Button(C("Salvar andar", "SaveAs"), GUILayout.Height(26f)))
                {
                    var fr = FloorTools.SelectedOrAny();
                    if (fr != null) { ApplyMeta(fr); if (FloorTools.SaveFloor(fr)) ShowNotification(new GUIContent("Andar salvo: " + fr.floorId)); }
                    else EditorUtility.DisplayDialog("Criador de Andares", "Gere ou abra um andar primeiro.", "OK");
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (classIds.Length > 0)
                {
                    int ci = Mathf.Max(0, System.Array.IndexOf(classIds, s.testClass));
                    ci = EditorGUILayout.Popup(ci, classNames, GUILayout.Width(110f));
                    s.testClass = classIds[ci];
                }
                GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
                if (GUILayout.Button(C("Testar andar (Play) · herói nível " + s.recLevel, "PlayButton"), GUILayout.Height(26f)))
                {
                    var fr = FloorTools.SelectedOrAny();
                    if (fr != null) { ApplyMeta(fr); FloorTools.TestFloor(fr, s.recLevel, s.testClass); }
                    else EditorUtility.DisplayDialog("Criador de Andares", "Gere ou abra um andar primeiro.", "OK");
                }
                GUI.backgroundColor = old;
                EditorGUILayout.EndHorizontal();
                Help("Testar salva o andar, abre a cena Drakantus e entra no Play direto no andar. Seu save é copiado antes e devolvido ao sair do Play.");

                if (GUILayout.Button("Carregar opções do andar selecionado"))
                {
                    var fr = FloorTools.SelectedOrAny();
                    if (fr != null && !string.IsNullOrEmpty(fr.generatorSettings))
                    {
                        try { s = JsonUtility.FromJson<FloorSettings>(fr.generatorSettings); SavePrefs(); }
                        catch { Debug.LogWarning("[Drakantus] Opções do andar ilegíveis."); }
                    }
                }
                if (!string.IsNullOrEmpty(FloorGenerator.LastReport))
                    EditorGUILayout.HelpBox(FloorGenerator.LastReport, MessageType.Info);
            }
        }

        void DoGenerate(bool newSeed)
        {
            if (newSeed) s.seed = Random.Range(1, 999999);
            SavePrefs();
            RefreshLists();
            var fr = FloorTools.GenerateInScene(s.Clone());
            if (fr != null) ShowNotification(new GUIContent("Andar gerado: " + fr.name));
        }

        /// <summary>Copia nome/dificuldade/etc. da janela para o andar antes de salvar (sem regerar).</summary>
        void ApplyMeta(FloorRoot fr)
        {
            if (fr.floorId != s.FloorId) return;   // outro andar selecionado: não mexe
            Undo.RecordObject(fr, "Dados do andar");
            fr.floorName = string.IsNullOrEmpty(s.floorName) ? fr.floorName : s.floorName;
            fr.subtitle = string.IsNullOrEmpty(s.subtitle) ? "Andar " + s.floor.ToString("00") + " · " + fr.floorName : s.subtitle;
            fr.difficulty = s.difficulty;
            fr.recommendedLevel = s.recLevel;
            fr.next = s.next ?? "";
            EditorUtility.SetDirty(fr);
        }

        // ================================================================== aba Andares
        void FloorsTab()
        {
            FloorRegistry.Reload();
            EditorGUILayout.Space(4f);
            Help("Andares que o jogo conhece (FloorRegistry). Os andares 1 e 2 são fixos (LevelBuilder).");
            foreach (var f in FloorRegistry.List())
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Andar " + f.floor.ToString("00") + " · " + f.name + (f.builtIn ? "  (fixo)" : ""), EditorStyles.boldLabel);
                EditorGUILayout.LabelField("id " + f.id + " · nível " + f.recommendedLevel + " · dificuldade " + f.difficulty + (string.IsNullOrEmpty(f.biome) ? "" : " · " + f.biome)
                    + (FloorRegistry.CanLoad(f.id) ? "" : "  (SEM PREFAB!)"), EditorStyles.miniLabel);
                if (!f.builtIn)
                {
                    EditorGUILayout.BeginHorizontal();
                    string scene = FloorKit.FloorsDir + "/Andar_" + f.floor.ToString("00") + ".unity";
                    GUI.enabled = File.Exists(scene);
                    if (GUILayout.Button("Abrir cena") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
                        var fr = FloorTools.FindRoot(f.id);
                        if (fr != null) { Selection.activeGameObject = fr.gameObject; FloorTools.Frame(fr); FloorTools.PreviewLighting(fr); }
                    }
                    GUI.enabled = true;
                    if (GUILayout.Button("Remover da lista") && EditorUtility.DisplayDialog("Remover andar", "Remover " + f.id + " da lista e apagar o prefab salvo? (a cena fica)", "Remover", "Cancelar"))
                    {
                        FloorTools.Unregister(f.id);
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(6f);
            Help("Para a janela da Torre (UI): FloorRegistry.List() devolve id, name, subtitle, floor, recommendedLevel; entrar com Game.I.EnterTower(id).");
        }

        // ================================================================== aba Retoque (paleta)
        struct PalItem
        {
            public string id, label, icon;
            public PalItem(string id, string label, string icon = "Prefab Icon") { this.id = id; this.label = label; this.icon = icon; }
        }

        List<PalItem> Items(int tab, BiomeDef b)
        {
            var l = new List<PalItem>();
            switch (tab)
            {
                case 0:
                    l.Add(new PalItem("piso", "Piso 4x4", "Terrain Icon"));
                    l.Add(new PalItem("piso_alto", "Piso elevado (+1,2 m)", "Terrain Icon"));
                    l.Add(new PalItem("parede", "Parede", "BoxCollider Icon"));
                    l.Add(new PalItem("parede_baixa", "Parede baixa", "BoxCollider Icon"));
                    l.Add(new PalItem("pilar", "Pilar", "BoxCollider Icon"));
                    l.Add(new PalItem("colisor", "Bloqueio invisível", "BoxCollider Icon"));
                    break;
                case 1:
                    AddDeco(l, "trees", b.trees, "Árvore");
                    AddDeco(l, "rocks", b.rocks, "Rocha");
                    AddDeco(l, "props", b.props, "Objeto");
                    AddDeco(l, "ruins", b.ruins, "Ruína");
                    AddDeco(l, "special", b.special, "Especial");
                    AddDeco(l, "scatter", b.scatter, "Chão");
                    AddDeco(l, "backdrop", b.backdrop, "Fundo");
                    break;
                case 2:
                    l.Add(new PalItem("tocha_parede", "Tocha de parede", "Light Icon"));
                    l.Add(new PalItem("tocha", "Tocha de pé", "Light Icon"));
                    l.Add(new PalItem("braseiro", "Braseiro", "Light Icon"));
                    l.Add(new PalItem("braseiro_sombra", "Braseiro c/ sombra (máx 2)", "Light Icon"));
                    l.Add(new PalItem("poste", "Poste com lanterna", "Light Icon"));
                    l.Add(new PalItem("luz", "Luz ambiente", "Light Icon"));
                    break;
                case 3:
                    l.Add(new PalItem("espetos_escondidos", "Espetos escondidos", "console.warnicon"));
                    l.Add(new PalItem("espetos", "Espetos à vista (ciclo)", "console.warnicon"));
                    l.Add(new PalItem("fogo", "Jato de fogo", "console.warnicon"));
                    l.Add(new PalItem("flechas", "Lançador de flechas (ciclo)", "console.warnicon"));
                    l.Add(new PalItem("placa", "Placa de pressão", "console.warnicon"));
                    break;
                case 4:
                    l.Add(new PalItem("porta_grade", "Porta de grade (aberta)", "BoxCollider Icon"));
                    l.Add(new PalItem("porta_grade_fechada", "Porta de grade (fechada)", "BoxCollider Icon"));
                    l.Add(new PalItem("porta_pedra", "Porta trancada (pedra)", "BoxCollider Icon"));
                    l.Add(new PalItem("parede_secreta", "Parede secreta (E)", "BoxCollider Icon"));
                    l.Add(new PalItem("alavanca", "Alavanca", "Prefab Icon"));
                    l.Add(new PalItem("gatilho_arena", "Gatilho de arena 16x16", "Prefab Icon"));
                    break;
                case 5:
                    l.Add(new PalItem("fx_veneno", "Névoa venenosa (dano)", "ParticleSystem Icon"));
                    foreach (FloorFxKind k in System.Enum.GetValues(typeof(FloorFxKind)))
                        l.Add(new PalItem("fx_" + k, k.ToString(), "ParticleSystem Icon"));
                    break;
                case 6:
                case 7:
                case 8:
                    foreach (var d in GameData.Enemies.Values)
                    {
                        bool mini = d.id.StartsWith("mb_");
                        if (tab == 6 && !d.boss) l.Add(new PalItem("enemy:" + d.id, d.name, "GameObject Icon"));
                        else if (tab == 7 && d.boss && mini) l.Add(new PalItem("mini:" + d.id, d.name, "GameObject Icon"));
                        else if (tab == 8 && d.boss && !mini) l.Add(new PalItem("boss:" + d.id, d.name, "GameObject Icon"));
                    }
                    break;
                case 9:
                    l.Add(new PalItem("bau", "Baú comum", "Prefab Icon"));
                    l.Add(new PalItem("bau_raro", "Baú raro", "Prefab Icon"));
                    l.Add(new PalItem("bau_sinal", "Baú de recompensa (aparece com sinal)", "Prefab Icon"));
                    break;
                case 10:
                    l.Add(new PalItem("escada12", "Escada 1,2 m", "Terrain Icon"));
                    l.Add(new PalItem("escada24", "Escada 2,4 m", "Terrain Icon"));
                    l.Add(new PalItem("rampa12", "Rampa lisa 1,2 m", "Terrain Icon"));
                    l.Add(new PalItem("plataforma", "Plataforma 8x4 com escada", "Terrain Icon"));
                    break;
                case 11:
                    l.Add(new PalItem("santuario", "Santuário (fim do andar)", "Light Icon"));
                    l.Add(new PalItem("entrada", "Ponto de entrada", "GameObject Icon"));
                    l.Add(new PalItem("portal", "Portal: sair da Torre", "SceneAsset Icon"));
                    break;
            }
            return l;
        }

        static void AddDeco(List<PalItem> l, string cat, DecoDef[] list, string prefix)
        {
            if (list == null) return;
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null) l.Add(new PalItem("deco:" + cat + ":" + i, prefix + ": " + list[i].id, "Prefab Icon"));
        }

        void PaletteTab()
        {
            var fr = FloorTools.SelectedOrAny();
            EditorGUILayout.Space(4f);
            if (fr == null)
            {
                EditorGUILayout.HelpBox("Nenhum andar na cena. Gere um andar (aba Gerar) ou abra uma cena Assets/Floors/Andar_XX.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("Andar: " + fr.name + " (" + fr.floorId + ")", EditorStyles.boldLabel);
            if (biomeIds.Length > 0)
            {
                if (string.IsNullOrEmpty(palBiome)) palBiome = fr.biome;
                int bi = Mathf.Max(0, System.Array.IndexOf(biomeIds, palBiome));
                bi = EditorGUILayout.Popup("Bioma das peças", bi, biomeNames);
                palBiome = biomeIds[bi];
            }
            var b = Biomes.Get(palBiome) ?? new BiomeDef();
            palSnap = EditorGUILayout.Toggle(new GUIContent("Encaixar na grade (4 m)", "Pisos, paredes, portas e escadas encaixam nas células do gerador"), palSnap);

            palTab = GUILayout.SelectionGrid(palTab, PalTabs, 3, GUILayout.Height(84f));
            EditorGUILayout.Space(4f);

            if (palTab == 6 || palTab == 7 || palTab == 8)
            {
                if (palTab == 6) palElite = EditorGUILayout.Toggle("Elite", palElite);
                EditorGUILayout.LabelField("Habilidades extras:", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i < EnemyAbilities.AllIds.Length; i++)
                {
                    if (i == 4) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
                    palAbil[i] = GUILayout.Toggle(palAbil[i], EnemyAbilities.Nome(EnemyAbilities.AllIds[i]), "Button", GUILayout.Height(20f));
                }
                EditorGUILayout.EndHorizontal();
                Help("Chefes e mini-chefes já têm habilidades próprias (bosses.json); as marcadas aqui são somadas.");
            }

            var items = Items(palTab, b);
            if (items.Count == 0) Help("(nada nesta categoria para o bioma escolhido)");
            for (int i = 0; i < items.Count; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                for (int k = i; k < i + 2 && k < items.Count; k++)
                {
                    var it = items[k];
                    bool on = armed == it.id;
                    var old = GUI.backgroundColor;
                    if (on) GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
                    if (GUILayout.Button(C(it.label, it.icon), GUILayout.Height(24f), GUILayout.MinWidth(120f)))
                    {
                        if (on) armed = null;
                        else { armed = it.id; armedLabel = it.label; }
                        SceneView.RepaintAll();
                    }
                    GUI.backgroundColor = old;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6f);
            if (!string.IsNullOrEmpty(armed))
                EditorGUILayout.HelpBox("Colocando: " + armedLabel + " (giro " + armedYaw + "°)\nClique na Scene View · Shift+clique repete · R gira 45° · Esc cancela", MessageType.None);
            else Help("Escolha uma peça e clique na Scene View para colocar. Shift+clique coloca várias. R gira 45°. Esc cancela.");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Ligações (placas, alavancas, arenas → portas, armadilhas, baús)", EditorStyles.miniBoldLabel);
            if (GUILayout.Button(C("Ligar seleção: 1º selecionado = fonte → demais = alvos", "Prefab Icon")))
                LinkSelection();
            if (GUILayout.Button(C("Atualizar luz/névoa da prévia", "Light Icon"))) FloorTools.PreviewLighting(fr);
        }

        void LinkSelection()
        {
            var src = Selection.activeGameObject;
            if (src == null || Selection.gameObjects.Length < 2)
            {
                EditorUtility.DisplayDialog("Ligar", "Selecione primeiro a FONTE (placa, alavanca ou gatilho) e depois, com Ctrl/Cmd, os ALVOS (portas, armadilhas, baús).", "OK");
                return;
            }
            var targets = new List<FloorReceiver>();
            foreach (var g in Selection.gameObjects)
            {
                if (g == src) continue;
                var r = g.GetComponentInParent<FloorReceiver>();
                if (r != null) targets.Add(r);
            }
            if (targets.Count == 0) { EditorUtility.DisplayDialog("Ligar", "Nenhum alvo válido (Door, SpikeTrap, FireTrap, ArrowTrap, ChestMarker).", "OK"); return; }
            Undo.RecordObject(src, "Ligar");
            var link = src.GetComponent<TriggerLink>();
            if (link == null) link = Undo.AddComponent<TriggerLink>(src);
            Undo.RecordObject(link, "Ligar");
            foreach (var t in targets) if (!link.targets.Contains(t)) link.targets.Add(t);
            EditorUtility.SetDirty(link);
            EditorSceneManager.MarkSceneDirty(src.scene);
            ShowNotification(new GUIContent("Ligado a " + targets.Count + " alvo(s)"));
        }

        // ------------------------------------------------------------------ Scene View
        void OnScene(SceneView sv)
        {
            if (string.IsNullOrEmpty(armed) || mainTab != 1) return;
            var e = Event.current;
            int ctrl = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(ctrl);
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape) { armed = null; e.Use(); Repaint(); sv.Repaint(); return; }
                if (e.keyCode == KeyCode.R) { armedYaw = Mathf.Repeat(armedYaw + 45f, 360f); e.Use(); Repaint(); }
            }
            var fr = FloorTools.SelectedOrAny();
            if (fr == null) return;
            if (!RayToWorld(e.mousePosition, out var hit)) return;
            if (palSnap && SnapItem(armed)) hit = Snap(fr, hit);

            Handles.color = new Color(1f, 0.85f, 0.3f);
            Handles.DrawWireDisc(hit, Vector3.up, 0.8f);
            Vector3 fwd = Quaternion.Euler(0f, armedYaw, 0f) * Vector3.forward;
            Handles.DrawLine(hit, hit + fwd * 1.6f);
            Handles.Label(hit + Vector3.up * 1.8f, armedLabel + "  " + armedYaw + "°");

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                Place(armed, fr, hit, armedYaw);
                if (!e.shift) { armed = null; Repaint(); }
                e.Use();
            }
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) sv.Repaint();
        }

        static bool RayToWorld(Vector2 mouse, out Vector3 hit)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            Physics.SyncTransforms();
            if (Physics.Raycast(ray, out var h, 3000f, ~0, QueryTriggerInteraction.Ignore)) { hit = h.point; return true; }
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float d)) { hit = ray.GetPoint(d); return true; }
            hit = Vector3.zero;
            return false;
        }

        static bool SnapItem(string id)
        {
            return id == "piso" || id == "piso_alto" || id == "parede" || id == "parede_baixa" || id == "colisor" ||
                   id.StartsWith("porta") || id == "parede_secreta" || id.StartsWith("escada") || id.StartsWith("rampa") || id.StartsWith("espetos");
        }

        /// <summary>Encaixa no centro da célula de 4 m do gerador (largura do andar = boundsSize.x).</summary>
        static Vector3 Snap(FloorRoot fr, Vector3 world)
        {
            Vector3 l = fr.transform.InverseTransformPoint(world);
            int gw = Mathf.Max(1, Mathf.RoundToInt(fr.boundsSize.x / FloorKit.TILE));
            float ox = gw * 0.5f - 0.5f;
            float i = Mathf.Round(l.x / FloorKit.TILE + ox);
            float j = Mathf.Round(l.z / FloorKit.TILE - 0.5f);
            l.x = (i - ox) * FloorKit.TILE;
            l.z = (j + 0.5f) * FloorKit.TILE;
            return fr.transform.TransformPoint(l);
        }

        string[] SelectedAbilities()
        {
            var l = new List<string>();
            for (int i = 0; i < palAbil.Length; i++) if (palAbil[i]) l.Add(EnemyAbilities.AllIds[i]);
            return l.ToArray();
        }

        void Place(string id, FloorRoot fr, Vector3 world, float yaw)
        {
            var b = Biomes.Get(palBiome) ?? Biomes.Get(fr.biome) ?? new BiomeDef();
            FloorPieces.biome = b;
            FloorPieces.rng = new System.Random(Random.Range(1, 999999));
            FloorKit.count = 0;
            var root = fr.transform;
            Vector3 p = root.InverseTransformPoint(world);
            Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            float wallH = Mathf.Max(2.2f, b.wallHeight);
            GameObject g = null;
            Transform G(string n) => FloorKit.Group(root, n);

            switch (id)
            {
                case "piso": g = FloorPieces.FloorTile(G("Chao"), p, p.y, FloorKit.TILE, false); break;
                case "piso_alto":
                {
                    g = FloorPieces.FloorTile(G("Chao"), p, p.y + 1.2f, FloorKit.TILE, false);
                    FloorKit.AddCollider(g, 1f, 0f);
                    break;
                }
                case "parede": g = FloorPieces.WallBlock(G("Paredes"), p, p.y - 0.5f, p.y + wallH, FloorKit.TILE, true); break;
                case "parede_baixa": g = FloorPieces.WallBlock(G("Paredes"), p, p.y - 0.5f, p.y + 1.1f, FloorKit.TILE, false); break;
                case "pilar":
                {
                    g = FloorKit.Box("bricks_B", G("Paredes"), p + Vector3.up * 1.6f, new Vector3(1.4f, 3.2f, 1.4f), 0f, new Color(0.55f, 0.36f, 0.3f), FloorKit.Hex(b.wallTint));
                    FloorKit.AddCollider(g, 1f, 0f);
                    FloorKit.Occluder(g);
                    break;
                }
                case "colisor": g = FloorKit.ColliderBox(G("Colisores"), p + Vector3.up * 2f, new Vector3(4f, 4f, 4f), yaw, "Bloqueio"); break;

                case "tocha_parede": g = FloorPieces.WallTorch(G("Luzes"), new Vector3(p.x, 0f, p.z), fwd, p.y); break;
                case "tocha": g = FloorPieces.StandingTorch(G("Luzes"), p); break;
                case "braseiro": g = FloorPieces.Brazier(G("Luzes"), p, false); break;
                case "braseiro_sombra": g = FloorPieces.Brazier(G("Luzes"), p, true); break;
                case "poste": g = FloorPieces.Lantern(G("Luzes"), p); break;
                case "luz": g = FloorPieces.AmbientLight(G("Luzes"), p, FloorKit.Hex(b.torchColor), 10f, 1.6f); break;

                case "espetos_escondidos": g = FloorPieces.MakeSpikes(G("Armadilhas"), p, true, 3f, 12f); break;
                case "espetos": g = FloorPieces.MakeSpikes(G("Armadilhas"), p, false, 3f, 12f); break;
                case "fogo": g = FloorPieces.MakeFireTrap(G("Armadilhas"), p, 10f, false); break;
                case "flechas": g = FloorPieces.MakeArrowTrap(G("Armadilhas"), p + Vector3.up * 1.1f, fwd, 8f, false); break;
                case "placa": g = FloorPieces.MakePlate(G("Armadilhas"), p, false); break;

                case "porta_grade": g = FloorPieces.MakeDoor(G("Portas_Alavancas"), p, fwd, side, 4f, wallH, "grade", true).gameObject; break;
                case "porta_grade_fechada": g = FloorPieces.MakeDoor(G("Portas_Alavancas"), p, fwd, side, 4f, wallH, "grade", false).gameObject; break;
                case "porta_pedra": g = FloorPieces.MakeDoor(G("Portas_Alavancas"), p, fwd, side, 4f, wallH, "pedra", false).gameObject; break;
                case "parede_secreta":
                {
                    var d = FloorPieces.MakeDoor(G("Portas_Alavancas"), p, fwd, side, 4f, wallH, "secreta", false);
                    var lv = d.gameObject.AddComponent<Drakantus.Lever>();
                    lv.hidden = true; lv.once = true; lv.radius = 2.8f; lv.label = "[E] Examinar a parede rachada";
                    FloorPieces.Link(d.gameObject, d);
                    g = d.gameObject;
                    break;
                }
                case "alavanca": g = FloorPieces.MakeLever(G("Portas_Alavancas"), p, fwd).gameObject; break;
                case "gatilho_arena": g = FloorPieces.MakeRoomClear(G("Gatilhos"), p, new Vector3(16f, 6f, 16f)).gameObject; break;

                case "fx_veneno": g = FloorPieces.MakeFog(G("Armadilhas"), p, 3f, 6f); break;

                case "bau": g = FloorPieces.MakeChest(G("Baus"), p, 1, false).gameObject; break;
                case "bau_raro": g = FloorPieces.MakeChest(G("Baus"), p, 2, false).gameObject; break;
                case "bau_sinal": g = FloorPieces.MakeChest(G("Baus"), p, 2, true).gameObject; break;

                case "escada12": g = FloorPieces.MakeStairs(G("Escadas"), p, yaw, 1.2f, 4f); break;
                case "escada24": g = FloorPieces.MakeStairs(G("Escadas"), p, yaw, 2.4f, 4f); break;
                case "rampa12": g = FloorPieces.MakeStairs(G("Escadas"), p, yaw, 1.2f, 4f, false); break;
                case "plataforma": g = FloorPieces.MakePlatform(G("Escadas"), p, yaw, 8f, 4f, 1.2f); break;

                case "santuario": g = FloorPieces.MakeSanctuary(G("Santuario_Portais"), p); break;
                case "entrada": g = FloorPieces.MakeSpawn(G("Santuario_Portais"), p, "entrance"); break;
                case "portal": g = FloorPieces.MakePortal(G("Santuario_Portais"), p); break;

                default:
                {
                    if (id.StartsWith("fx_"))
                    {
                        string kn = id.Substring(3);
                        FloorFxKind k;
                        if (System.Enum.TryParse(kn, out k))
                        {
                            Color c = k == FloorFxKind.Chama ? new Color(1f, 0.55f, 0.15f) : k == FloorFxKind.Fumaca ? new Color(0.5f, 0.5f, 0.5f, 0.3f)
                                : k == FloorFxKind.Nevoa ? new Color(0.8f, 0.85f, 0.9f, 0.16f) : k == FloorFxKind.Brasas ? new Color(1f, 0.5f, 0.15f)
                                : k == FloorFxKind.Vagalumes ? new Color(0.8f, 1f, 0.45f) : k == FloorFxKind.Areia ? new Color(0.9f, 0.8f, 0.6f, 0.35f) : Color.white;
                            Vector3 size = k == FloorFxKind.Chama ? Vector3.one : new Vector3(12f, 4f, 12f);
                            g = FloorKit.Fx(G("Efeitos"), p + Vector3.up * (k == FloorFxKind.Chama ? 0.2f : 2f), k, size, c, k == FloorFxKind.Chama ? 14 : 50).gameObject;
                        }
                    }
                    else if (id.StartsWith("deco:"))
                    {
                        var parts = id.Split(':');
                        DecoDef[] list = null;
                        switch (parts[1])
                        {
                            case "trees": list = b.trees; break;
                            case "rocks": list = b.rocks; break;
                            case "props": list = b.props; break;
                            case "ruins": list = b.ruins; break;
                            case "special": list = b.special; break;
                            case "scatter": list = b.scatter; break;
                            case "backdrop": list = b.backdrop; break;
                        }
                        int idx;
                        if (list != null && int.TryParse(parts[2], out idx) && idx >= 0 && idx < list.Length)
                            g = FloorPieces.Deco(G("Decoracao"), list[idx], p, yaw);
                    }
                    else if (id.StartsWith("enemy:") || id.StartsWith("mini:") || id.StartsWith("boss:"))
                    {
                        string eid = id.Substring(id.IndexOf(':') + 1);
                        EnemyRole role = id.StartsWith("boss:") ? EnemyRole.Chefe : id.StartsWith("mini:") ? EnemyRole.MiniChefe : (palElite ? EnemyRole.Elite : EnemyRole.Normal);
                        string group = role == EnemyRole.Chefe ? "Chefes" : role == EnemyRole.MiniChefe ? "MiniChefes" : "Inimigos";
                        var m = FloorPieces.MakeEnemy(G(group), p, eid, role, SelectedAbilities(), role == EnemyRole.Elite ? 1.2f : 1f);
                        m.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                        g = m.gameObject;
                    }
                    break;
                }
            }
            if (g == null) { Debug.LogWarning("[Drakantus] Peça não colocada: " + id); return; }
            Undo.RegisterCreatedObjectUndo(g, "Colocar " + armedLabel);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = g;
            EditorSceneManager.MarkSceneDirty(fr.gameObject.scene);
        }
    }
}
