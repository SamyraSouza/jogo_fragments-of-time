using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Poeira flutuando no feixe da lanterna: partículas pequenas e quase invisíveis espalhadas dentro do cone de luz.</summary>
    public class FlashlightDust : MonoBehaviour
    {
        public float rate = 80f;
        public float maxLength = 9f;
        public float alpha = 0.55f;

        Light lightRef;
        ParticleSystem ps;
        bool wasOn;
        static Texture2D dot;

        void Start()
        {
            lightRef = GetComponent<Light>();
            if (lightRef == null) { enabled = false; return; }

            var go = new GameObject("PoeiraDoFeixe");
            go.transform.SetParent(transform, false);
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.07f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.93f, 0.8f, alpha), new Color(1f, 1f, 1f, alpha * 0.6f));
            main.maxParticles = 600;
            main.gravityModifier = -0.002f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.ConeVolume;
            shape.angle = Mathf.Min(lightRef.spotAngle * 0.5f, 30f);
            shape.radius = 0.04f;
            shape.length = Mathf.Min(Mathf.Max(lightRef.range, 4f), maxLength);

            var em = ps.emission;
            em.rateOverTime = 0f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.2f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1.15f), new Keyframe(1f, 0.7f)));

            var r = ps.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = Dot() };
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
        }

        void Update()
        {
            if (ps == null) return;
            bool on = lightRef.enabled && lightRef.gameObject.activeInHierarchy;
            var em = ps.emission;
            em.rateOverTime = on ? rate : 0f;
            if (!on && wasOn) ps.Clear();
            wasOn = on;
        }

        static Texture2D Dot()
        {
            if (dot != null) return dot;
            const int s = 32;
            dot = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    dot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            dot.Apply();
            return dot;
        }
    }
}
