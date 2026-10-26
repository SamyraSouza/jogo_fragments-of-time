using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Respiração e balanço de passos da câmera.</summary>
    [DefaultExecutionOrder(1000)]
    public class CameraSway : MonoBehaviour
    {
        /// <summary>0..1: tensão (a máquina tremendo).</summary>
        public static float Stress;

        [Header("Respiração")]
        public float breathHz = 0.28f;
        public float breathHeight = 0.006f;
        public float breathPitch = 0.22f;

        [Header("Passos")]
        public float stepDip = 0.03f;
        public float stepRoll = 0.8f;
        public float stepNod = 0.45f;
        public float stepSide = 0.012f;

        Vector3 appliedPos, lastPos;
        Quaternion appliedRot = Quaternion.identity, lastRot = Quaternion.identity;
        bool hasApplied;
        float breathPhase;
        float walkBlend;
        SimpleFPSController fps;

        void Awake()
        {
            fps = GetComponentInParent<SimpleFPSController>();
            var pre = gameObject.GetComponent<CameraSwayPre>();
            if (pre == null) pre = gameObject.AddComponent<CameraSwayPre>();
            pre.sway = this;
        }

        /// <summary>Tira o desvio do quadro anterior, se ninguém mexeu na câmera desde então.</summary>
        public void RemoveApplied()
        {
            if (!hasApplied) return;
            if ((transform.localPosition - lastPos).sqrMagnitude < 1e-10f) transform.localPosition -= appliedPos;
            if (Quaternion.Angle(transform.localRotation, lastRot) < 0.02f) transform.localRotation = transform.localRotation * Quaternion.Inverse(appliedRot);
            hasApplied = false;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float stress = Mathf.Clamp01(Stress);

            breathPhase += dt * Mathf.PI * 2f * breathHz * Mathf.Lerp(1f, 3.2f, stress);
            float amp = Mathf.Lerp(1f, 2.4f, stress);
            float br = Mathf.Sin(breathPhase);
            float br2 = Mathf.Sin(breathPhase * 0.5f + 1.3f);
            Vector3 pos = new Vector3(0f, br * breathHeight * amp, 0f);
            Vector3 rot = new Vector3(br * breathPitch * amp, br2 * 0.12f * amp, br2 * 0.16f * amp);

            var bob = ArmsWalkBob.Instance;
            bool walking = bob != null && (fps == null || fps.enabled);
            walkBlend = Mathf.MoveTowards(walkBlend, walking ? Mathf.Clamp01(bob.Amount) : 0f, dt * 6f);
            if (walkBlend > 0.001f)
            {
                float s = Mathf.Sin(bob.Phase);
                float dip = Mathf.Abs(s);
                pos += new Vector3(s * stepSide, -dip * stepDip, 0f) * walkBlend;
                rot += new Vector3(dip * stepNod, s * 0.35f, -s * stepRoll) * walkBlend;
            }

            appliedPos = pos;
            appliedRot = Quaternion.Euler(rot);
            transform.localPosition += appliedPos;
            transform.localRotation = transform.localRotation * appliedRot;
            lastPos = transform.localPosition;
            lastRot = transform.localRotation;
            hasApplied = true;
        }

        void OnDisable()
        {
            RemoveApplied();
        }
    }

    /// <summary>Roda ANTES do controlador do jogador (ordem -1000) para retirar o desvio do quadro anterior.</summary>
    [DefaultExecutionOrder(-1000)]
    public class CameraSwayPre : MonoBehaviour
    {
        [HideInInspector] public CameraSway sway;
        void Update() { if (sway != null) sway.RemoveApplied(); }
    }
}
