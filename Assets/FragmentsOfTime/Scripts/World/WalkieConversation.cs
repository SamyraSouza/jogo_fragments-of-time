using System.Collections;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Conversa do Marco com o Leo pelo walkie-talkie, no começo do prólogo.</summary>
    public class WalkieConversation : MonoBehaviour
    {
        [Header("Áudio do Leo")]
        public string clipName = "sinal_ruim";
        public float leoVolume = 0.95f;

        [Header("Chiado do rádio (só a partir da distância em que a máquina começa a tremer forte)")]
        public float staticBase = 0.2f;
        public float staticMax = 0.8f;
        public float staticCurve = 1.1f;
        public float duckWhileLeoSpeaks = 0.45f;

        [Header("Legenda")]
        public bool subtitles = true;

        class Line
        {
            public bool marco;
            public float minProximity;
            public string text;
            public bool glitch;
            public bool lastLeo;
        }

        static readonly Line[] Talk =
        {
            new Line { marco = true,  text = "Leo, aqui é o Marco. Está me ouvindo? Câmbio." },
            new Line { marco = false, text = "Alto e claro, Marco. Como está aí dentro?" },
            new Line { marco = true,  text = "Entrei na caverna. Está bem escuro, mas o caminho está livre." },
            new Line { marco = false, text = "Ótimo. Já achou alguma coisa?" },
            new Line { marco = true,  text = "Alguns sinais estavam estranhos nos sensores, por isso resolvi vir ver de perto." },
            new Line { marco = false, text = "Estranhos como? Aqui em cima os aparelhos também enlouqueceram hoje cedo." },
            new Line { marco = true,  text = "Leituras de energia que não deviam existir aqui. E parecem vir do fundo da câmara." },
            new Line { marco = false, text = "Marco, não vá sozinho. Espera a equipe chegar, câmbio." },
            new Line { marco = true,  text = "Relaxa, é só uma olhada rápida. Tem uma luz lá no fundo." },
            new Line { marco = false, text = "Tá bom, mas fica comigo no rádio o tempo todo. Entendeu?", lastLeo = true },
            new Line { marco = true,  text = "Entendido. Vou me aproximar com cuidado." },
        };

        static readonly Line[] Calls =
        {
            new Line { marco = true, minProximity = 0f,    text = "Leo? Está me ouvindo? Câmbio." },
            new Line { marco = true, minProximity = 0.03f, text = "Leo, não estou te ouvindo. Só chiado." },
            new Line { marco = true, minProximity = 0.08f, text = "Leo, está me ouvindo? Responde!" },
            new Line { marco = true, minProximity = 0.15f, text = "Câmbio, câmbio! Leo, não estou ouvindo nada!" },
            new Line { marco = true, minProximity = 0.25f, text = "Leo? Está me ouvindo? Repete, só chiado aqui!" },
            new Line { marco = true, minProximity = 0.4f,  text = "Droga... Leo, se estiver me ouvindo, responde!" },
            new Line { marco = true, minProximity = 0.6f,  text = "Leo... não estou ouvindo você. Leo?" },
        };

        TimeMachineAura aura;
        PunchAnimator punch;
        AudioSource radio, hiss, click;
        AudioClip clickClip;
        float duck = 1f, duckTarget = 1f, maxK;
        bool staticOn;
        bool leoSilent;

        public void Begin(TimeMachineAura machineAura)
        {
            aura = machineAura;
            radio = NewSource("LeoRadio", false);
            click = NewSource("RadioClique", false);
            hiss = NewSource("RadioChiado", true);
            hiss.clip = MakeHissLoop();
            hiss.volume = 0f;
            hiss.Play();
            clickClip = MakeClick();
            StartCoroutine(Run());
        }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.spatialBlend = 0f;
            a.loop = loop;
            a.playOnAwake = false;
            return a;
        }

        static float Duration(string text) { return Mathf.Max(2f, text.Length * 0.062f); }

        bool SignalLost { get { return aura != null && aura.Proximity > 0.001f; } }

        IEnumerator Run()
        {
            float wait = 0f;
            while (punch == null && wait < 8f)
            {
                punch = FindFirstObjectByType<PunchAnimator>();
                wait += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(1.2f);

            AudioClip walkieAudio = Resources.Load<AudioClip>("Walkie/" + clipName);
            if (walkieAudio == null) Debug.LogWarning("[Walkie] não achei Assets/Resources/Walkie/" + clipName + ".mp3");

            foreach (Line line in Talk)
            {
                if (SignalLost) break;
                yield return Say(line, walkieAudio, true);
                yield return new WaitForSeconds(0.35f);
            }

            staticOn = true;
            radio.Stop();
            if (punch != null) punch.SetTalking(false);
            leoSilent = true;
            yield return new WaitForSeconds(1.1f);

            foreach (Line line in Calls)
            {
                while (aura != null && aura.Proximity < line.minProximity) yield return null;
                yield return Say(line, null, false);
                yield return new WaitForSeconds(0.6f);
            }
        }

        IEnumerator Say(Line line, AudioClip walkieAudio, bool interruptible)
        {
            float length = Duration(line.text);
            string who = line.marco ? "Marco" : "Leo";
            if (subtitles && HUD.Instance != null) HUD.Instance.ShowDialogue(who + ": " + line.text, length + 0.4f, line.marco);

            if (line.marco)
            {
                if (punch != null && punch.CanTalk) punch.SetTalking(true);
                yield return Wait(0.55f, interruptible);
                Click(0.35f);
                yield return Wait(length, interruptible);
                Click(0.3f);
                yield return Wait(0.2f, interruptible);
                if (punch != null) punch.SetTalking(false);
                yield return Wait(0.4f, interruptible);
            }
            else
            {
                duckTarget = duckWhileLeoSpeaks;
                if (walkieAudio != null) yield return PlayLeo(walkieAudio, length, line.glitch, interruptible);
                else yield return Wait(length, interruptible);
                duckTarget = 1f;
                if (line.lastLeo) staticOn = true;
                yield return Wait(0.3f, interruptible);
            }
        }

        IEnumerator Wait(float seconds, bool interruptible)
        {
            float t0 = Time.time;
            while (Time.time - t0 < seconds)
            {
                if (interruptible && SignalLost) yield break;
                yield return null;
            }
        }

        IEnumerator PlayLeo(AudioClip clip, float seconds, bool glitch, bool interruptible)
        {
            seconds = Mathf.Min(seconds, clip.length);
            radio.clip = clip;
            radio.time = 0f;
            radio.volume = leoVolume;
            radio.pitch = glitch ? 0.9f : 1f;
            radio.Play();
            float fadeLen = Mathf.Min(0.5f, seconds * 0.3f);
            float t0 = Time.time;
            while (Time.time - t0 < seconds)
            {
                if (interruptible && SignalLost) break;
                float t = Time.time - t0;
                float v = leoVolume * Mathf.Clamp01((seconds - t) / fadeLen);
                if (glitch) v *= Mathf.PerlinNoise(Time.time * 9f, 4.2f) < 0.35f ? 0.05f : 1f;
                radio.volume = v;
                yield return null;
            }
            radio.Stop();
            radio.pitch = 1f;
        }

        void Click(float volume)
        {
            if (click != null && clickClip != null) click.PlayOneShot(clickClip, volume);
        }

        void Update()
        {
            if (hiss == null || aura == null) return;
            if (SignalLost) staticOn = true;
            maxK = Mathf.Max(maxK, Mathf.Clamp01(aura.Proximity));
            float level = staticOn ? Mathf.Lerp(staticBase, staticMax, Mathf.Pow(maxK, staticCurve)) : 0f;
            hiss.volume = Mathf.MoveTowards(hiss.volume, level, Time.deltaTime * 1.5f);
            hiss.pitch = 1f;
        }

        /// <summary>Corta a conversa (o arrasto começou).</summary>
        public void Abort()
        {
            StopAllCoroutines();
            if (radio != null) radio.Stop();
            if (punch != null) punch.SetTalking(false);
            staticOn = true;
            leoSilent = true;
        }

        /// <summary>O chiado passa a sair do walkie caído no chão (som 3D), sem nunca parar de tocar.</summary>
        public void AttachHissTo(Transform t)
        {
            if (hiss == null || t == null) return;
            hiss.transform.SetParent(t, false);
            hiss.transform.localPosition = Vector3.zero;
            hiss.spatialBlend = 0.55f;
            hiss.rolloffMode = AudioRolloffMode.Linear;
            hiss.minDistance = 2f;
            hiss.maxDistance = 90f;
        }

        void OnDestroy()
        {
            if (punch != null) punch.SetTalking(false);
        }

        public static AudioClip MakeHissLoop()
        {
            const int rate = 22050;
            const int n = rate * 4, fade = 2200;
            var buf = new float[n + fade];
            var rng = new System.Random(21);
            float lp = 0f, hp = 0f, prev = 0f;
            float pop = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.55f * (w - lp);
                hp = 0.985f * (hp + lp - prev);
                prev = lp;
                float v = hp * 1.6f;
                if (rng.NextDouble() < 0.00008) pop = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.8f;
                pop *= 0.92f;
                float swell = 1f;
                buf[i] = Mathf.Clamp((v + pop) * swell * 0.55f, -1f, 1f);
            }
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                data[i] = buf[i];
                if (i < fade) data[i] = Mathf.Lerp(buf[n + i], buf[i], i / (float)fade);
            }
            var clip = AudioClip.Create("RadioHiss", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip MakeClick()
        {
            const int rate = 22050;
            int n = (int)(0.09f * rate);
            var d = new float[n];
            var rng = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Exp(-i / (n * 0.22f));
                d[i] = ((float)(rng.NextDouble() * 2.0 - 1.0) * 0.8f + Mathf.Sin(i * 0.9f) * 0.3f) * env;
            }
            var clip = AudioClip.Create("RadioClick", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
