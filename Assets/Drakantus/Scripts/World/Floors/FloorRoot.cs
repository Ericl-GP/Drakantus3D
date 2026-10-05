using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Raiz de um andar criado no editor (Drakantus > Criador de Andares). Fica no objeto "Andar_XX"
    /// e é salvo como prefab em Resources/Floors/&lt;floorId&gt;.prefab.
    /// No jogo, LevelBuilder.Build instancia o prefab e chama <see cref="Build"/>, que monta o LevelInfo
    /// a partir dos marcadores filhos (entrada, inimigos, santuário, portais). Depois que o Game cria os
    /// inimigos, o FloorRoot aplica dificuldade, elites e habilidades (EnemyAbilities).
    /// </summary>
    public class FloorRoot : MonoBehaviour
    {
        [Header("Andar")]
        public string floorId = "tower_f3";
        public string title = "Torre de Aster";
        public string subtitle = "Andar 03";
        public string floorName = "Andar 03";
        public int floor = 3;
        [Tooltip("Id do próximo andar. Vazio = procura o andar de número seguinte no FloorRegistry.")]
        public string next = "";
        [Range(1, 10)] public int difficulty = 1;
        public int recommendedLevel = 1;

        [Header("Bioma")]
        public string biome = "masmorra";
        public string biome2 = "";
        public string music = "dungeon";

        [Header("Luz e névoa")]
        public Color ambient = new Color(0.25f, 0.27f, 0.32f);
        public Color sun = new Color(1f, 0.95f, 0.85f);
        public float sunIntensity = 1f;
        public float sunPitch = 50f, sunYaw = -30f;
        public bool dark;
        public Color fog = new Color(0.2f, 0.22f, 0.26f);
        public float fogDensity = 0.02f;
        [Tooltip("Tinta aplicada aos inimigos comuns deste andar (branco = normal).")]
        public Color enemyTint = Color.white;

        [Header("Limites (local)")]
        public Vector3 boundsCenter = new Vector3(0, 3, 40);
        public Vector3 boundsSize = new Vector3(60, 12, 80);

        [Header("Gerador (não mexa)")]
        public int seed;
        [TextArea(2, 6)] public string generatorSettings = "";

        /// <summary>true quando foi instanciado pelo jogo (LevelBuilder). Marcadores só agem nesse caso.</summary>
        [System.NonSerialized] public bool runtime;

        public float HpMult => 1f + (Mathf.Clamp(difficulty, 1, 10) - 1) * 0.12f;
        public float DmgMult => 1f + (Mathf.Clamp(difficulty, 1, 10) - 1) * 0.08f;
        public float XpMult => 1f + (Mathf.Clamp(difficulty, 1, 10) - 1) * 0.10f;

        // ------------------------------------------------------------------ utilidades para os componentes do andar
        public static FloorRoot Of(Component c) => c != null ? c.GetComponentInParent<FloorRoot>() : null;

        /// <summary>O componente está num andar carregado pelo jogo?</summary>
        public static bool IsRuntime(Component c)
        {
            var r = Of(c);
            return r != null && r.runtime && Application.isPlaying;
        }

        /// <summary>Multiplicador de dano das armadilhas (dificuldade do andar).</summary>
        public static float TrapMult(Component c)
        {
            var r = Of(c);
            return r != null ? r.DmgMult : 1f;
        }

        /// <summary>Jogador vivo e capaz de levar dano (ou null).</summary>
        public static Player LivePlayer()
        {
            var g = Game.I;
            if (g == null || g.player == null || g.Traveling) return null;
            var p = g.player;
            if (p.state == "dead") return null;
            return p;
        }

        /// <summary>Aplica dano ao jogador com o som certo. Retorna o resultado de Player.TakeDamage.</summary>
        public static string HurtPlayer(float amount, Vector3 from)
        {
            var p = LivePlayer();
            if (p == null || amount <= 0f) return "ignored";
            string r = p.TakeDamage(amount, from);
            if (r == "hit") Sfx.Play("player_hurt", p.transform.position);
            else if (r == "blocked") Sfx.Play("block", p.transform.position);
            else if (r == "parried") Sfx.Play("parry", p.transform.position);
            return r;
        }

        // ------------------------------------------------------------------ montagem (chamado pelo LevelBuilder)
        /// <summary>Instancia o prefab do andar dentro de root e devolve o LevelInfo.</summary>
        public static LevelInfo Build(GameObject prefab, Transform root)
        {
            var go = Object.Instantiate(prefab, root);
            go.name = prefab.name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var fr = go.GetComponent<FloorRoot>();
            if (fr == null)
            {
                Debug.LogWarning("[Drakantus] O prefab do andar " + prefab.name + " não tem FloorRoot; usando valores padrão.");
                fr = go.AddComponent<FloorRoot>();
                fr.floorId = prefab.name;
            }
            fr.runtime = true;
            LevelDecor.SetSunAngles(fr.sunPitch, fr.sunYaw);
            return fr.MakeInfo();
        }

        /// <summary>LevelInfo a partir dos campos e dos marcadores filhos.</summary>
        public LevelInfo MakeInfo()
        {
            var info = new LevelInfo
            {
                id = string.IsNullOrEmpty(floorId) ? name : floorId,
                title = title,
                subtitle = subtitle,
                kind = "dungeon",
                floor = floor,
                next = next,
                music = string.IsNullOrEmpty(music) ? "dungeon" : music,
                ambient = ambient,
                sun = sun,
                sunIntensity = sunIntensity,
                dark = dark,
                fog = fog,
                fogDensity = fogDensity,
            };
            if (string.IsNullOrEmpty(info.next)) info.next = FloorRegistry.NextAfter(floor);
            if (info.next == info.id) info.next = "";
            info.bounds = new Bounds(transform.TransformPoint(boundsCenter), boundsSize);

            foreach (var sp in GetComponentsInChildren<FloorSpawnPoint>(true))
            {
                if (!sp.gameObject.activeInHierarchy) continue;
                string key = string.IsNullOrEmpty(sp.key) ? "entrance" : sp.key;
                info.spawns[key] = sp.transform.position;
            }
            if (!info.spawns.ContainsKey("default"))
            {
                if (info.spawns.TryGetValue("entrance", out var e)) info.spawns["default"] = e;
                else
                {
                    // sem ponto de entrada: começa no centro dos limites
                    Vector3 c = info.bounds.center; c.y = 0.1f;
                    info.spawns["default"] = c;
                    Debug.LogWarning("[Drakantus] Andar " + info.id + " sem ponto de entrada (FloorSpawnPoint).");
                }
            }
            if (!info.spawns.ContainsKey("entrance")) info.spawns["entrance"] = info.spawns["default"];

            foreach (var m in GetComponentsInChildren<EnemySpawnMarker>(true))
            {
                if (!m.gameObject.activeInHierarchy || string.IsNullOrEmpty(m.enemyId)) continue;
                info.enemies.Add(new EnemySpawn(m.enemyId, m.transform.position, m.scale > 0f ? m.scale : 1f));
            }
            foreach (var p in GetComponentsInChildren<FloorPortalMarker>(true))
            {
                if (!p.gameObject.activeInHierarchy) continue;
                info.portals.Add(new PortalSpawn(p.transform.position, string.IsNullOrEmpty(p.targetMap) ? "town" : p.targetMap,
                    p.targetSpawn, p.label, p.radius > 0f ? p.radius : 1.6f));
            }
            foreach (var s in GetComponentsInChildren<SanctuaryMarker>(true))
            {
                if (!s.gameObject.activeInHierarchy) continue;
                info.hasSanctuary = true;
                info.sanctuary = s.transform.position;
                break;
            }
            return info;
        }

        // ------------------------------------------------------------------ depois que o Game criou os inimigos
        void Start()
        {
            if (!runtime || !Application.isPlaying) return;
            StartCoroutine(AfterSpawn());
        }

        IEnumerator AfterSpawn()
        {
            // o Game cria os inimigos logo depois do LevelBuilder.Build; espera 1 frame por segurança
            yield return null;
            var g = Game.I;
            if (g == null) yield break;

            var all = new List<Enemy>();
            foreach (var e in g.enemies)
                if (e != null && !e.dead && (g.levelRoot == null || e.transform.IsChildOf(g.levelRoot))) all.Add(e);

            // dificuldade para todos
            foreach (var e in all)
            {
                EnemyAbilities.Scale(e, HpMult, DmgMult, XpMult);
                if (!e.def.boss && enemyTint != Color.white && e.visual != null)
                {
                    Color t = string.IsNullOrEmpty(e.def.tint) ? Color.white : U.Hex(e.def.tint);
                    e.visual.SetTint(t * enemyTint);
                }
            }

            // marcador → inimigo mais próximo com o mesmo id
            var claimed = new HashSet<Enemy>();
            foreach (var m in GetComponentsInChildren<EnemySpawnMarker>(true))
            {
                if (!m.gameObject.activeInHierarchy) continue;
                Enemy best = null;
                float bd = 2.5f;
                foreach (var e in all)
                {
                    if (claimed.Contains(e) || e.def == null || e.def.id != m.enemyId) continue;
                    float d = U.Flat(e.transform.position - m.transform.position).magnitude;
                    if (d < bd) { bd = d; best = e; }
                }
                if (best == null) continue;
                claimed.Add(best);
                m.spawned = best;

                var prof = BossProfiles.Get(m.enemyId);
                bool elite = m.role == EnemyRole.Elite;
                bool hasAbil = (m.abilities != null && m.abilities.Length > 0) || prof != null || elite;
                if (m.hpMult > 0f && Mathf.Abs(m.hpMult - 1f) > 0.01f)
                {
                    best.maxHp *= m.hpMult;
                    best.hp = best.maxHp;
                }
                if (!hasAbil) continue;

                var list = new List<string>();
                var phaseList = new List<string>();
                bool phases = m.role == EnemyRole.Chefe;
                if (prof != null)
                {
                    if (prof.abilities != null) list.AddRange(prof.abilities);
                    if (prof.phaseAbilities != null) phaseList.AddRange(prof.phaseAbilities);
                    phases |= prof.phases;
                }
                if (m.abilities != null)
                    foreach (var a in m.abilities)
                        if (!string.IsNullOrEmpty(a) && !list.Contains(a)) list.Add(a);
                var ab = EnemyAbilities.Attach(best, list, phaseList, elite, phases);
                if (ab != null && prof != null && !string.IsNullOrEmpty(prof.aura)) ab.SetAura(U.Hex(prof.aura));
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
            Gizmos.DrawWireCube(transform.TransformPoint(boundsCenter), boundsSize);
        }
    }
}
