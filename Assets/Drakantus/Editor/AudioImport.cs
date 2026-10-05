using UnityEditor;
using UnityEngine;

namespace Drakantus.EditorTools
{
    /// <summary>
    /// Configura a importação dos WAV gerados por Tools/audio/gen_audio.py:
    /// - efeitos: DecompressOnLoad + ADPCM (baixa latência, pouca CPU)
    /// - music_*: Streaming + Vorbis qualidade 0.6 (pouca memória)
    /// Todos forçados para mono.
    /// </summary>
    public class AudioImport : AssetPostprocessor
    {
        const string AudioDir = "Assets/Drakantus/Resources/Audio/";

        // mude o número para forçar reimportação quando estas regras mudarem
        public override uint GetVersion() { return 1; }

        void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(AudioDir)) return;

            var importer = assetImporter as AudioImporter;
            if (importer == null) return;

            bool music = System.IO.Path.GetFileNameWithoutExtension(path).StartsWith("music_");

            importer.forceToMono = true;
            importer.loadInBackground = music;

            AudioImporterSampleSettings s = importer.defaultSampleSettings; // struct: pega, altera, reatribui
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            s.quality = music ? 0.6f : 1f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = s;
        }
    }
}
