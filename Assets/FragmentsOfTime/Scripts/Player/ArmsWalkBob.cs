using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Balanço natural dos braços ao andar: sobem e descem a cada passo e balançam de leve para os lados.</summary>
    public class ArmsWalkBob : MonoBehaviour
    {
        [Header("Balanço (metros)")]
        public float vertical = 0.06f;
        public float sideways = 0.035f;
        public float forwardBack = 0.02f;
        public float beamSway = 9f;
        public Transform beam;
        public float tilt = 4f;

        [Header("Ritmo")]
        public float stepsPerMeter = 0.9f;
        public float fullSpeed = 4f;
        public float blendSpeed = 8f;

        /// <summary>Instância ativa: os passos (PlayerFootsteps) e a câmera (CameraSway) seguem o mesmo ritmo.</summary>
        public static ArmsWalkBob Instance { get; private set; }
        public float Phase { get { return phase; } }
        public float Amount { get { return amount; } }

        Transform body;
        Vector3 lastPos, applied;
        Quaternion appliedRot = Quaternion.identity, appliedBeam = Quaternion.identity;
        float phase, amount;

        void Start()
        {
            Instance = this;
            Rigidbody rb = GetComponentInParent<Rigidbody>();
            body = rb != null ? rb.transform : transform.root;
            lastPos = body.position;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 delta = body.position - lastPos;
            lastPos = body.position;
            delta.y = 0f;
            float speed = delta.magnitude / dt;
            if (speed > 30f) speed = 0f;

            amount = Mathf.MoveTowards(amount, Mathf.Clamp01(speed / fullSpeed), blendSpeed * dt);
            phase += speed * stepsPerMeter * Mathf.PI * dt;

            transform.localPosition -= applied;
            applied = new Vector3(
                Mathf.Sin(phase) * sideways,
                -Mathf.Abs(Mathf.Sin(phase)) * vertical,
                Mathf.Cos(phase * 2f) * forwardBack) * amount;
            transform.localPosition += applied;

            transform.localRotation *= Quaternion.Inverse(appliedRot);
            appliedRot = Quaternion.Euler(-Mathf.Abs(Mathf.Sin(phase)) * tilt * amount, 0f, Mathf.Sin(phase) * tilt * amount);
            transform.localRotation *= appliedRot;

            if (beam != null)
            {
                beam.localRotation *= Quaternion.Inverse(appliedBeam);
                appliedBeam = Quaternion.Euler(-Mathf.Abs(Mathf.Sin(phase)) * beamSway * amount, Mathf.Sin(phase) * beamSway * 0.7f * amount, 0f);
                beam.localRotation *= appliedBeam;
            }
        }

        void OnDisable()
        {
            transform.localPosition -= applied;
            transform.localRotation *= Quaternion.Inverse(appliedRot);
            if (beam != null) beam.localRotation *= Quaternion.Inverse(appliedBeam);
            appliedBeam = Quaternion.identity;
            applied = Vector3.zero;
            appliedRot = Quaternion.identity;
        }
    }
}
