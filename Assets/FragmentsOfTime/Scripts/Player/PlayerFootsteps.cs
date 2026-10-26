using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Passos do jogador usando a gravação Resources/Sfx/passos.mp3.</summary>
    public class PlayerFootsteps : MonoBehaviour
    {
        [Range(0f, 1f)] public float volume = 0.22f;
        public float minAmount = 0.2f;
        public float groundRay = 2.2f;
        public string clipPath = "Sfx/passos";
        public int maxSlices = 24;

        AudioSource src, loopSrc;
        SimpleFPSController fps;
        Transform player;
        readonly List<AudioClip> steps = new List<AudioClip>();
        AudioClip whole;
        int lastStep = int.MinValue, lastPick = -1, foot;
        float wholeStepsPerSecond = 2f;

        void Start()
        {
            player = transform.root;
            fps = GetComponentInParent<SimpleFPSController>();
            src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.playOnAwake = false;

            whole = Resources.Load<AudioClip>(clipPath);
            if (whole == null) { Debug.LogWarning("[Passos] não achei Assets/Resources/" + clipPath + ".mp3"); enabled = false; return; }
            Slice(whole);
            if (steps.Count < 3)
            {
                loopSrc = gameObject.AddComponent<AudioSource>();
                loopSrc.clip = whole; loopSrc.loop = true; loopSrc.spatialBlend = 0f; loopSrc.volume = 0f; loopSrc.Play();
                Debug.Log("[Passos] não separei passos; usando o áudio inteiro em loop");
            }
        }

        void Slice(AudioClip clip)
        {
            int ch = clip.channels, len = clip.samples;
            var raw = new float[len * ch];
            if (!clip.GetData(raw, 0))
            {
                Debug.LogWarning("[Passos] falha ao ler as amostras (rode Rift Of Time > 13. Configurar áudio dos passos).");
                return;
            }
            var mono = new float[len];
            for (int i = 0; i < len; i++)
            {
                float s = 0f;
                for (int c = 0; c < ch; c++) s += raw[i * ch + c];
                mono[i] = s / ch;
            }
            int sr = clip.frequency;

            int win = Mathf.Max(64, sr / 160);
            int frames = len / win;
            var env = new float[frames];
            float max = 0f, mean = 0f;
            for (int f = 0; f < frames; f++)
            {
                float e = 0f;
                int o = f * win;
                for (int i = 0; i < win; i++) e += mono[o + i] * mono[o + i];
                env[f] = Mathf.Sqrt(e / win);
                if (env[f] > max) max = env[f];
                mean += env[f];
            }
            if (frames == 0 || max < 1e-4f) return;
            mean /= frames;
            float thr = Mathf.Max(max * 0.28f, mean * 2.2f);

            var onsets = new List<int>();
            int minGap = Mathf.RoundToInt(0.24f * sr / win);
            int last = -minGap;
            for (int f = 1; f < frames - 1; f++)
            {
                bool rising = env[f] > thr && env[f - 1] <= thr * 0.75f;
                if (rising && f - last >= minGap) { onsets.Add(f); last = f; }
            }
            if (onsets.Count < 3) return;

            wholeStepsPerSecond = (onsets.Count - 1) / Mathf.Max(0.5f, (onsets[onsets.Count - 1] - onsets[0]) * win / (float)sr);

            int pre = Mathf.RoundToInt(0.02f * sr);
            for (int k = 0; k < onsets.Count && steps.Count < maxSlices; k++)
            {
                int a = Mathf.Max(0, onsets[k] * win - pre);
                int b = k + 1 < onsets.Count ? onsets[k + 1] * win - pre : Mathf.Min(len, a + Mathf.RoundToInt(0.5f * sr));
                b = Mathf.Min(b, a + Mathf.RoundToInt(0.6f * sr));
                if (b - a < Mathf.RoundToInt(0.12f * sr)) continue;
                var seg = new float[b - a];
                System.Array.Copy(mono, a, seg, 0, seg.Length);
                int fi = Mathf.RoundToInt(0.004f * sr), fo = Mathf.RoundToInt(0.05f * sr);
                float peak = 1e-5f;
                for (int i = 0; i < seg.Length; i++)
                {
                    if (i < fi) seg[i] *= i / (float)fi;
                    int fromEnd = seg.Length - 1 - i;
                    if (fromEnd < fo) seg[i] *= fromEnd / (float)fo;
                    peak = Mathf.Max(peak, Mathf.Abs(seg[i]));
                }
                float g = 0.9f / peak;
                for (int i = 0; i < seg.Length; i++) seg[i] *= g;
                var sc = AudioClip.Create("passo_" + k, seg.Length, 1, sr, false);
                sc.SetData(seg, 0);
                steps.Add(sc);
            }
            Debug.Log("[Passos] " + steps.Count + " passos separados do áudio (" + (len / (float)sr).ToString("F1") + " s, " + onsets.Count + " batidas)");
        }

        Vector3 lastPos;
        bool hasLast;
        float stepDist;

        // os passos seguem a distância andada de verdade (não dependem do balanço dos braços): um passo a cada ~0,8 m
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 d = player.position - lastPos;
            lastPos = player.position;
            if (!hasLast) { hasLast = true; return; }
            float vertical = Mathf.Abs(d.y) / dt;
            d.y = 0f;
            float speed = d.magnitude / dt;
            bool walking = (fps == null || fps.enabled || ScriptedWalk.Active) && speed > 0.35f && speed < 12f && vertical < 2f;
            float amount = Mathf.Clamp01(speed / 4f);

            if (loopSrc != null)
            {
                float target = walking ? volume * Mathf.Lerp(0.6f, 1f, amount) : 0f;
                loopSrc.volume = Mathf.MoveTowards(loopSrc.volume, target, dt * 4f);
                loopSrc.pitch = Mathf.Clamp(speed / 0.8f / Mathf.Max(0.5f, wholeStepsPerSecond), 0.7f, 1.6f);
                return;
            }

            if (!walking || steps.Count == 0) { stepDist = Mathf.Min(stepDist, 0.4f); return; }
            stepDist += speed * dt;
            if (stepDist < 0.8f) return;
            stepDist -= 0.8f;

            int pick;
            do { pick = Random.Range(0, steps.Count); } while (steps.Count > 1 && pick == lastPick);
            lastPick = pick;
            foot ^= 1;
            src.panStereo = foot == 0 ? -0.1f : 0.1f;
            src.pitch = Random.Range(0.96f, 1.04f);
            src.PlayOneShot(steps[pick], volume * Mathf.Lerp(0.6f, 1f, amount) * Random.Range(0.85f, 1f));
        }

        bool Grounded()
        {
            Vector3 o = player.position + Vector3.up * 0.3f;
            foreach (RaycastHit h in Physics.RaycastAll(o, Vector3.down, groundRay, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(player) || h.normal.y < 0.5f) continue;
                return true;
            }
            return false;
        }
    }
}
