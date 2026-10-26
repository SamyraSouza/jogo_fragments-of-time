using UnityEditor;
using UnityEngine;

namespace RiftOfTime.EditorTools
{
    /// <summary>Configura os áudios do menu: a música (intro-menu.mp3) em "Streaming" (abre rápido, sem carregar tudo na memória) e</summary>
    public static class MenuAudioImportSettings
    {
        [InitializeOnLoadMethod]
        static void Auto() { EditorApplication.delayCall += Apply; }

        public static void Apply()
        {
            Set("Assets/Resources/Sfx/intro-menu.mp3", AudioClipLoadType.Streaming);
            Set("Assets/Resources/Sfx/sino-iniciar.mp3", AudioClipLoadType.DecompressOnLoad);
        }

        static void Set(string path, AudioClipLoadType type)
        {
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) return;
            var s = imp.defaultSampleSettings;
            if (s.loadType == type) return;
            s.loadType = type;
            if (type == AudioClipLoadType.DecompressOnLoad) s.preloadAudioData = true;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
            Debug.Log("[Menu] áudio configurado: " + path + " (" + type + ")");
        }
    }
}
