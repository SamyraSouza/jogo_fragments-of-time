using System;
using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Gera em código os sons citados no storyboard (gemido da múmia, tambores, estalo elétrico.</summary>
    public static class ProceduralAudio
    {
        const int SR = 22050;
        const float TwoPi = Mathf.PI * 2f;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static AudioClip Get(string id)
        {
            AudioClip c;
            if (cache.TryGetValue(id, out c) && c != null) return c;
            c = Build(id);
            cache[id] = c;
            return c;
        }

        static AudioClip Make(string name, float seconds, Func<float, float> gen)
        {
            int n = Mathf.Max(1, (int)(SR * seconds));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(gen(i / (float)SR), -1f, 1f);
            var clip = AudioClip.Create("rot_" + name, n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise() { return UnityEngine.Random.value * 2f - 1f; }

        static AudioClip Build(string id)
        {
            switch (id)
            {
                case "groan":
                {
                    float ph = 0f;
                    return Make(id, 1.5f, t =>
                    {
                        float f = 62f + 10f * Mathf.Sin(TwoPi * 4f * t) - 18f * t;
                        ph += TwoPi * f / SR;
                        float s = Mathf.Sin(ph) + 0.5f * Mathf.Sin(2f * ph) + 0.25f * Mathf.Sin(3f * ph);
                        float env = Mathf.Clamp01(t / 0.15f) * Mathf.Exp(-t * 1.8f);
                        return s * env * 0.5f + Noise() * 0.03f * env;
                    });
                }
                case "drum":
                {
                    float ph = 0f;
                    return Make(id, 0.6f, t =>
                    {
                        float f = 55f + 90f * Mathf.Exp(-t * 25f);
                        ph += TwoPi * f / SR;
                        return Mathf.Sin(ph) * Mathf.Exp(-t * 8f) + Noise() * 0.15f * Mathf.Exp(-t * 60f);
                    });
                }
                case "drums":
                {
                    return Make(id, 2f, t =>
                    {
                        float v = 0f;
                        float[] hits = { 0f, 0.5f, 1.0f, 1.25f, 1.5f };
                        foreach (float h in hits)
                        {
                            float lt = t - h;
                            if (lt >= 0f && lt < 0.4f)
                            {
                                float f = 52f + 80f * Mathf.Exp(-lt * 25f);
                                v += Mathf.Sin(TwoPi * f * lt) * Mathf.Exp(-lt * 9f);
                            }
                        }
                        return v * 0.6f;
                    });
                }
                case "slash":
                {
                    float y = 0f;
                    return Make(id, 0.28f, t =>
                    {
                        float n = Noise();
                        y += 0.35f * (n - y);
                        float env = Mathf.Sin(Mathf.Clamp01(t / 0.28f) * Mathf.PI);
                        return y * env * 0.9f;
                    });
                }
                case "hit":
                {
                    float ph = 0f;
                    return Make(id, 0.35f, t =>
                    {
                        ph += TwoPi * (90f - 60f * t) / SR;
                        return Mathf.Sin(ph) * Mathf.Exp(-t * 12f) + Noise() * 0.5f * Mathf.Exp(-t * 35f);
                    });
                }
                case "chime":
                {
                    return Make(id, 2.2f, t =>
                    {
                        float env = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t * 1.6f);
                        float s = Mathf.Sin(TwoPi * 440f * t) + Mathf.Sin(TwoPi * 554.37f * t) + Mathf.Sin(TwoPi * 659.25f * t) + 0.4f * Mathf.Sin(TwoPi * 880f * t);
                        return s * env * 0.22f;
                    });
                }
                case "crackle":
                {
                    float last = 0f;
                    return Make(id, 4f, t =>
                    {
                        float k = t / 4f;
                        float density = 0.0004f + 0.02f * k * k;
                        if (UnityEngine.Random.value < density) last = Noise();
                        last *= 0.93f;
                        return last * (0.3f + 0.7f * k) + Noise() * 0.04f * k;
                    });
                }
                case "heartbeat":
                {
                    return Make(id, 0.9f, t =>
                    {
                        float v = 0f;
                        float[] beats = { 0f, 0.28f };
                        for (int b = 0; b < beats.Length; b++)
                        {
                            float lt = t - beats[b];
                            if (lt >= 0f && lt < 0.25f) v += Mathf.Sin(TwoPi * 48f * lt) * Mathf.Exp(-lt * 18f) * (b == 0 ? 1f : 0.7f);
                        }
                        return v;
                    });
                }
                case "rumble":
                {
                    float y = 0f;
                    return Make(id, 2.5f, t =>
                    {
                        y += 0.04f * (Noise() - y);
                        float env = Mathf.Clamp01(t / 0.1f) * Mathf.Exp(-t * 1.3f);
                        return (y * 6f + Mathf.Sin(TwoPi * 32f * t) * 0.4f) * env;
                    });
                }
                case "ambient":
                {
                    return Make(id, 8f, t =>
                    {
                        float trem = 0.7f + 0.3f * Mathf.Sin(TwoPi * 0.25f * t);
                        float s = Mathf.Sin(TwoPi * 110f * t) * 0.5f
                                + Mathf.Sin(TwoPi * 164.875f * t) * 0.3f
                                + Mathf.Sin(TwoPi * 220.125f * t) * 0.25f
                                + Mathf.Sin(TwoPi * 277.25f * t) * 0.12f * (0.5f + 0.5f * Mathf.Sin(TwoPi * 0.125f * t));
                        return s * trem * 0.35f;
                    });
                }
                case "tense":
                {
                    return Make(id, 4f, t =>
                    {
                        float pulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(TwoPi * 2f * t * 0.5f)), 3f);
                        float s = Mathf.Sin(TwoPi * 82.5f * t) * 0.6f + Mathf.Sin(TwoPi * 123.75f * t) * 0.35f + Mathf.Sin(TwoPi * 165f * t * (1f + 0.002f)) * 0.2f;
                        return s * (0.4f + 0.6f * pulse) * 0.5f;
                    });
                }
                case "calm":
                {
                    return Make(id, 8f, t =>
                    {
                        float slow = 0.6f + 0.4f * Mathf.Sin(TwoPi * 0.125f * t);
                        float s = Mathf.Sin(TwoPi * 262f * t) + Mathf.Sin(TwoPi * 330f * t) * 0.8f + Mathf.Sin(TwoPi * 392f * t) * 0.7f + Mathf.Sin(TwoPi * 524f * t) * 0.3f;
                        return s * slow * 0.12f;
                    });
                }
                case "portal":
                {
                    float ph = 0f;
                    return Make(id, 3f, t =>
                    {
                        float k = t / 3f;
                        ph += TwoPi * (80f + 900f * k * k) / SR;
                        return (Mathf.Sin(ph) * 0.6f + Noise() * 0.15f) * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.8f;
                    });
                }
                default:
                    return Make(id, 0.1f, t => 0f);
            }
        }
    }
}
