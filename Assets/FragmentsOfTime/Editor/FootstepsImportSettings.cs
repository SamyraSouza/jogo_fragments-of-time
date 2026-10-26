using UnityEditor;
using UnityEngine;

namespace RiftOfTime.EditorTools
{
    /// <summary>Configura Assets/Resources/Sfx/passos.mp3 para o jogo poder ler as amostras e recortar cada passo</summary>
    public static class FootstepsImportSettings
    {
        const string Path = "Assets/Resources/Sfx/passos.mp3";

        [InitializeOnLoadMethod]
        static void Auto()
        {
            EditorApplication.delayCall += Apply;
        }

        public static void Apply()
        {
            var imp = AssetImporter.GetAtPath(Path) as AudioImporter;
            if (imp == null) return;
            var s = imp.defaultSampleSettings;
            bool ok = s.loadType == AudioClipLoadType.DecompressOnLoad && imp.forceToMono && s.preloadAudioData;
            if (ok) return;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.8f;
            s.preloadAudioData = true;
            imp.defaultSampleSettings = s;
            imp.forceToMono = true;
            imp.SaveAndReimport();
            Debug.Log("[Passos] áudio configurado (Decompress On Load, mono): " + Path);
        }
    }
}
