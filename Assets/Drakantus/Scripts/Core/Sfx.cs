using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Sistema de áudio do Drakantus 3D (sem AudioMixer).
    /// - Efeitos: Sfx.Play("hit", posicao) — clipes em Resources/Audio/&lt;id&gt;.wav
    /// - Música:  Sfx.Music("town") — Resources/Audio/music_&lt;id&gt;.wav, crossfade equal-power (2 s) em loop; "" para.
    ///   Ids por área: town, guild, forest, crypt, desert, snow, ruins_city, lava, boss, boss_final, greed, menu
    ///   (apelidos: "title" = menu, "dungeon" = crypt). Sem arquivo próprio, cai num fallback (ver musicFallback).
    /// - Loop:    var s = Sfx.PlayLoop("charge_loop", pos); ... Sfx.StopLoop(s);
    /// Tudo é criado sob demanda num GameObject "Sfx" (DontDestroyOnLoad); pode ser chamado a qualquer momento.
    /// </summary>
    public static class Sfx
    {
        public static float MasterVolume = 1f, MusicVolume = 0.55f, SfxVolume = 0.9f;

        const int PoolSize = 24;
        const float MinInterval = 0.035f;   // mesmo id no máximo 1x a cada 35 ms
        const int MaxPerId = 4;             // no máximo 4 instâncias simultâneas do mesmo id

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> lastPlay = new Dictionary<string, float>();

        static GameObject root;
        static AudioSource[] pool;
        static string[] poolIds;
        static int nextIndex;

        static SfxRunner runner;
        static AudioSource musicA, musicB, musicActive;
        static string currentMusic = "";
        static Coroutine fadeRoutine;
        static bool fading;

        // Zera o estado estático ao entrar em Play (útil com "Enter Play Mode Options" sem domain reload)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            clips.Clear();
            lastPlay.Clear();
            root = null; pool = null; poolIds = null; nextIndex = 0;
            runner = null; musicA = musicB = musicActive = null;
            currentMusic = ""; fadeRoutine = null; fading = false;
        }

        // ------------------------------------------------------------------ efeitos
        public static void Play(string id, Vector3? pos = null, float volume = 1f, float pitchVar = 0.08f)
        {
            if (string.IsNullOrEmpty(id) || !Ensure()) return;
            AudioClip clip = GetClip(id);
            if (clip == null) return; // id sem som: ignora em silêncio

            float now = Time.unscaledTime;
            if (lastPlay.TryGetValue(id, out float last) && now - last < MinInterval) return;

            // conta instâncias tocando deste id
            int playing = 0;
            for (int i = 0; i < PoolSize; i++)
                if (poolIds[i] == id && pool[i] != null && pool[i].isPlaying) playing++;
            if (playing >= MaxPerId) return;

            AudioSource src = NextSource(out int index);
            if (src == null) return;
            lastPlay[id] = now;
            poolIds[index] = id;

            float idScale = (id == "step" || id == "footstep_stone") ? 0.45f : 1f;
            src.Stop();
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume * idScale * SfxVolume * MasterVolume);
            src.pitch = 1f + Random.Range(-pitchVar, pitchVar);

            if (pos.HasValue)
            {
                src.transform.position = pos.Value;
                src.spatialBlend = 0.6f;   // meio 3D: a câmera isométrica fica longe, não deixar sumir
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 4f;
                src.maxDistance = 40f;
            }
            else
            {
                src.spatialBlend = 0f;
                src.transform.localPosition = Vector3.zero;
            }
            src.Play();
        }

        static AudioSource NextSource(out int index)
        {
            // procura uma fonte livre a partir do índice rotativo; se todas ocupadas, rouba a próxima
            for (int k = 0; k < PoolSize; k++)
            {
                int i = (nextIndex + k) % PoolSize;
                if (pool[i] != null && !pool[i].isPlaying)
                {
                    nextIndex = (i + 1) % PoolSize;
                    index = i;
                    return pool[i];
                }
            }
            index = nextIndex;
            nextIndex = (nextIndex + 1) % PoolSize;
            return pool[index];
        }

        /// <summary>True se existe o clipe Resources/Audio/&lt;id&gt;.wav (usa o mesmo cache do Play).
        /// Útil para escolher um som específico com fallback: Sfx.Has("sk_" + id) ? "sk_" + id : "magic_cast".</summary>
        public static bool Has(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return GetClip(id) != null;
        }

        /// <summary>Toca um efeito em LOOP numa fonte própria (fora do pool), ex.: "charge_loop".
        /// Guarde o retorno e chame Sfx.StopLoop(fonte) ao terminar. Retorna null se o clipe não existir.</summary>
        public static AudioSource PlayLoop(string id, Vector3? pos = null, float volume = 1f)
        {
            if (string.IsNullOrEmpty(id) || !Ensure()) return null;
            AudioClip clip = GetClip(id);
            if (clip == null) return null;
            var go = new GameObject("sfx_loop_" + id);
            go.transform.SetParent(root.transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.dopplerLevel = 0f;
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume * SfxVolume * MasterVolume);
            if (pos.HasValue)
            {
                go.transform.position = pos.Value;
                s.spatialBlend = 0.6f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 4f;
                s.maxDistance = 40f;
            }
            s.Play();
            return s;
        }

        /// <summary>Para e descarta uma fonte criada por PlayLoop (aceita null).</summary>
        public static void StopLoop(AudioSource src)
        {
            if (src != null) Object.Destroy(src.gameObject);
        }

        static AudioClip GetClip(string name)
        {
            if (clips.TryGetValue(name, out AudioClip c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name); // null fica em cache: não tenta de novo
            clips[name] = c;
            return c;
        }

        // ------------------------------------------------------------------ música
        // Cadeia de fallback por área: tenta music_<id>, depois cada alternativa, na ordem.
        // Ex.: "guild" sem arquivo próprio cai na música da praça; "title" e "dungeon" são apelidos.
        static readonly Dictionary<string, string[]> musicFallback = new Dictionary<string, string[]>
        {
            { "menu",       new[] { "menu", "title" } },
            { "title",      new[] { "menu", "title" } },
            { "town",       new[] { "town" } },
            { "guild",      new[] { "guild", "town" } },
            { "forest",     new[] { "forest", "crypt", "dungeon" } },
            { "crypt",      new[] { "crypt", "dungeon" } },
            { "dungeon",    new[] { "crypt", "dungeon" } },
            { "desert",     new[] { "desert", "crypt", "dungeon" } },
            { "snow",       new[] { "snow", "forest", "crypt", "dungeon" } },
            { "ruins_city", new[] { "ruins_city", "crypt", "dungeon" } },
            { "lava",       new[] { "lava", "boss", "crypt", "dungeon" } },
            { "boss",       new[] { "boss" } },
            { "boss_final", new[] { "boss_final", "boss" } },
            { "greed",      new[] { "greed", "boss", "town" } },
        };

        /// <summary>Resolve um id de música (área) para o clipe existente, seguindo a cadeia de fallback.
        /// Ids desconhecidos tentam music_&lt;id&gt; e depois a música de masmorra.</summary>
        static AudioClip ResolveMusic(string id, out string resolved)
        {
            resolved = "";
            if (string.IsNullOrEmpty(id)) return null;
            string key = id.Trim().ToLowerInvariant();
            if (!musicFallback.TryGetValue(key, out string[] chain))
                chain = new[] { key, "crypt", "dungeon" };
            for (int i = 0; i < chain.Length; i++)
            {
                AudioClip c = GetClip("music_" + chain[i]);
                if (c != null) { resolved = chain[i]; return c; }
            }
            return null;
        }

        public static void Music(string id, float fade = 2f)
        {
            if (!Ensure()) return;
            // currentMusic guarda o id RESOLVIDO: "title" -> "menu" não reinicia a mesma faixa
            AudioClip clip = ResolveMusic(id, out string resolved);
            if (resolved == currentMusic && (resolved == "" || (musicActive != null && musicActive.isPlaying))) return;
            if (resolved == "" && (musicActive == null || !musicActive.isPlaying)) { currentMusic = ""; return; }
            currentMusic = resolved;

            if (fadeRoutine != null) { runner.StopCoroutine(fadeRoutine); fadeRoutine = null; }
            fadeRoutine = runner.StartCoroutine(Crossfade(clip, Mathf.Max(0f, fade)));
        }

        static float MusicTarget => Mathf.Clamp01(MusicVolume * MasterVolume);

        static IEnumerator Crossfade(AudioClip clip, float fade)
        {
            fading = true;
            AudioSource from = musicActive;
            AudioSource to = null;
            // crossfade interrompido + pedido de silêncio: a fonte "velha" que ainda soava é parada
            AudioSource other = (from == musicA) ? musicB : musicA;
            if (clip == null && other != null && other != from) { other.Stop(); other.clip = null; }
            if (clip != null)
            {
                to = (from == musicA) ? musicB : musicA;
                to.Stop();
                to.clip = clip;
                to.loop = true;
                to.volume = 0f;
                to.Play();
            }
            musicActive = to;
            float fromStart = from != null ? from.volume : 0f;

            // curva equal-power (seno/cosseno): a soma de potência fica constante, sem "buraco" no meio
            float t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime; // independe do hitstop (timeScale)
                float k = Mathf.Clamp01(t / fade);
                float a = k * Mathf.PI * 0.5f;
                if (to != null) to.volume = MusicTarget * Mathf.Sin(a);
                if (from != null) from.volume = fromStart * Mathf.Cos(a);
                yield return null;
            }
            if (to != null) to.volume = MusicTarget;
            if (from != null && from != to) { from.Stop(); from.clip = null; }
            fading = false;
            fadeRoutine = null;
        }

        // chamado pelo SfxRunner a cada frame: aplica mudanças de MusicVolume/MasterVolume
        internal static void Tick()
        {
            if (!fading && musicActive != null) musicActive.volume = MusicTarget;
        }

        // ------------------------------------------------------------------ infraestrutura
        static bool Ensure()
        {
            if (root != null) return true;
            if (!Application.isPlaying) return false; // não cria objetos no modo de edição

            root = new GameObject("Sfx");
            Object.DontDestroyOnLoad(root);
            runner = root.AddComponent<SfxRunner>();

            pool = new AudioSource[PoolSize];
            poolIds = new string[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("sfx_" + i);
                go.transform.SetParent(root.transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.dopplerLevel = 0f;
                s.spatialBlend = 0f;
                pool[i] = s;
            }

            musicA = CreateMusicSource("music_a");
            musicB = CreateMusicSource("music_b");
            musicActive = null;
            fading = false;
            fadeRoutine = null;
            if (!string.IsNullOrEmpty(currentMusic)) currentMusic = ""; // objeto foi recriado: reinicia estado
            return true;
        }

        static AudioSource CreateMusicSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 0f;
            s.priority = 0; // música nunca é cortada
            s.volume = 0f;
            return s;
        }
    }

    /// <summary>Helper interno: roda corrotinas de crossfade e sincroniza o volume da música.</summary>
    [AddComponentMenu("")]
    internal class SfxRunner : MonoBehaviour
    {
        void Update() { Sfx.Tick(); }
    }
}
