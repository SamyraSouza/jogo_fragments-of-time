using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Tremor de câmera (golpe recebido, colapso, portal).</summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        Vector3 basePos;
        float impulseAmp, impulseLeft, impulseTotal, continuous;
        bool shaking;

        void Awake()
        {
            Instance = this;
            basePos = transform.localPosition;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Tremor curto.</summary>
        public static void Kick(float amplitude, float seconds)
        {
            if (Instance != null) Instance.Shake(amplitude, seconds);
        }

        /// <summary>Tremor contínuo (0 desliga).</summary>
        public static void Hold(float amplitude)
        {
            if (Instance != null) Instance.continuous = amplitude;
        }

        public void Shake(float amplitude, float seconds)
        {
            impulseAmp = Mathf.Max(impulseAmp * (impulseLeft > 0f ? impulseLeft / impulseTotal : 0f), amplitude);
            impulseTotal = Mathf.Max(0.01f, seconds);
            impulseLeft = impulseTotal;
        }

        void LateUpdate()
        {
            float a = continuous;
            if (impulseLeft > 0f)
            {
                a += impulseAmp * (impulseLeft / impulseTotal);
                impulseLeft -= Time.deltaTime;
            }

            if (a <= 0.0005f)
            {
                if (shaking) { transform.localPosition = basePos; shaking = false; }
                return;
            }

            shaking = true;
            float t = Time.time * 28f;
            Vector3 off = new Vector3(
                Mathf.PerlinNoise(t, 0.3f) - 0.5f,
                Mathf.PerlinNoise(0.7f, t) - 0.5f,
                (Mathf.PerlinNoise(t, t) - 0.5f) * 0.5f) * 2f * a;
            transform.localPosition = basePos + off;
        }
    }
}
