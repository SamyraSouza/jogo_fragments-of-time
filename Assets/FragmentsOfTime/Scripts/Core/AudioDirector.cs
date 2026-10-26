using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Toca efeitos (2D) e loops com fade.</summary>
    public class AudioDirector : MonoBehaviour
    {
        class LoopInfo
        {
            public AudioSource source;
            public float target;
            public bool stopping;
        }

        public static AudioDirector Instance { get; private set; }

        AudioSource sfx;
        readonly Dictionary<string, LoopInfo> loops = new Dictionary<string, LoopInfo>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        public static AudioDirector Create(Transform parent)
        {
            var go = new GameObject("AudioDirector");
            go.transform.SetParent(parent, false);
            return go.AddComponent<AudioDirector>();
        }

        void Awake()
        {
            Instance = this;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
        }

        /// <summary>Atalho seguro: toca um som procedural pelo id (ver ProceduralAudio).</summary>
        public static void Sfx(string id, float volume = 1f)
        {
            if (Instance == null) return;
            // "hit" e "fall" usam os sons gravados (Resources/Sfx), se existirem; os outros são gerados por código
            AudioClip rec = id == "hit" ? Recorded("Sfx/impacto") : id == "mummy_hit" ? Recorded("Sfx/soco_mumia") : id == "fall" ? Recorded("Sfx/queda") : null;
            Instance.sfx.PlayOneShot(rec != null ? rec : ProceduralAudio.Get(id == "fall" || id == "mummy_hit" ? "hit" : id), volume);
        }

        static readonly System.Collections.Generic.Dictionary<string, AudioClip> recorded = new System.Collections.Generic.Dictionary<string, AudioClip>();
        static AudioClip Recorded(string path)
        {
            AudioClip c;
            if (!recorded.TryGetValue(path, out c) || c == null) { c = Resources.Load<AudioClip>(path); recorded[path] = c; }
            return c;
        }

        /// <summary>Toca um som gravado de Resources (ex.: "Sfx/boulder_thud").</summary>
        public static void Rec(string path, float volume = 1f)
        {
            AudioClip c = Recorded(path);
            if (c != null) Sfx(c, volume);
        }

        public static void Sfx(AudioClip clip, float volume = 1f)
        {
            if (Instance == null || clip == null) return;
            Instance.sfx.PlayOneShot(clip, volume);
        }

        public void Loop(string key, AudioClip clip, float volume, float fadeIn = 1f)
        {
            if (clip == null) return;
            LoopInfo li;
            if (!loops.TryGetValue(key, out li) || li.source == null)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = true;
                src.spatialBlend = 0f;
                src.volume = 0f;
                src.Play();
                li = new LoopInfo { source = src };
                loops[key] = li;
            }
            li.stopping = false;
            li.target = volume;
        }

        public void Loop(string key, string proceduralId, float volume, float fadeIn = 1f)
        {
            Loop(key, ProceduralAudio.Get(proceduralId), volume, fadeIn);
        }

        /// <summary>Para o loop NA HORA (sem fade): corta o som e remove a fonte.</summary>
        public void StopLoopNow(string key)
        {
            LoopInfo li;
            if (loops.TryGetValue(key, out li))
            {
                if (li.source != null) { li.source.Stop(); Destroy(li.source); }
                loops.Remove(key);
            }
        }

        public void StopLoop(string key)
        {
            LoopInfo li;
            if (loops.TryGetValue(key, out li)) { li.stopping = true; li.target = 0f; }
        }

        public void StopAllLoops(float fade)
        {
            foreach (var kv in loops) { kv.Value.stopping = true; kv.Value.target = 0f; }
        }

        void Update()
        {
            List<string> dead = null;
            foreach (var kv in loops)
            {
                LoopInfo li = kv.Value;
                if (li.source == null) { if (dead == null) dead = new List<string>(); dead.Add(kv.Key); continue; }
                li.source.volume = Mathf.MoveTowards(li.source.volume, li.target, Time.unscaledDeltaTime * 0.8f);
                if (li.stopping && li.source.volume <= 0.001f)
                {
                    Destroy(li.source);
                    if (dead == null) dead = new List<string>();
                    dead.Add(kv.Key);
                }
            }
            if (dead != null) foreach (string k in dead) loops.Remove(k);
        }
    }
}
