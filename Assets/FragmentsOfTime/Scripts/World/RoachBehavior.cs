using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Barata: fica parada no chão (com pequenos tremores) e, quando o feixe da lanterna do jogador a ilumina.</summary>
    public class RoachBehavior : MonoBehaviour
    {
        [Header("Fuga")]
        public float runSpeed = 0.9f;
        public float scaredSeconds = 2.2f;
        public float turnSpeed = 14f;

        [Header("Parada")]
        public float twitchEvery = 4f;
        public float obstacleCheck = 0.12f;

        Animator anim;
        Transform head, hip;
        Quaternion headFix = Quaternion.identity;
        // a lanterna é uma só: todas as baratas dividem a luz achada e a busca (eram 152 baratas varrendo todas as luzes da cena a cada segundo)
        static Light beam;
        static float nextBeamSearch;
        float scaredUntil, nextTwitch, twitchUntil, shuffleUntil;
        Vector3 dir;

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLower();
                if (head == null && n == "head3") head = t;
                if (hip == null && n == "hip") hip = t;
            }
        }

        void Start()
        {
            Vector3 v = Vector3.forward;
            if (head != null && hip != null)
            {
                v = transform.InverseTransformDirection(head.position - hip.position);
                v.y = 0f;
                v = v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
            }
            headFix = Quaternion.Inverse(Quaternion.LookRotation(v, Vector3.up));

            float yaw = Random.Range(0f, 360f);
            dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up) * headFix;
            nextTwitch = Time.time + Random.Range(0.5f, twitchEvery * 2f);
            if (anim != null) anim.speed = 0f;
        }

        void Update()
        {
            if ((beam == null || !beam.enabled) && Time.time >= nextBeamSearch)
            {
                nextBeamSearch = Time.time + 1f;
                var found = new System.Text.StringBuilder();
                foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include))
                {
                    if (!l.name.ToLower().Contains("flashlight")) continue;
                    found.Append("'" + l.name + "' (ligada=" + l.enabled + ") ");
                    if (beam == null || (l.enabled && !beam.enabled)) beam = l;
                }
                if (!loggedSearch)
                {
                    loggedSearch = true;
                    Debug.Log("[Barata] luz da lanterna: " + (beam != null ? found.ToString() + "-> usando '" + beam.name + "' em " + beam.transform.root.name + "/…/" + beam.name + ", alcance " + beam.range + ", ângulo " + beam.spotAngle : "NÃO ACHEI nenhuma luz com 'flashlight' no nome"));
                }
            }

            if (IsLit())
            {
                if (Time.time >= scaredUntil) PickFleeDirection();
                scaredUntil = Time.time + scaredSeconds;
            }

            bool running = Time.time < scaredUntil;
            if (running) Move(runSpeed, 2.4f);
            else Idle();
        }

        static float nextDebug;
        static bool loggedSearch;
        static float nextStateLog;

        bool IsLit()
        {
            if (beam == null || !beam.enabled || !beam.gameObject.activeInHierarchy)
            {
                if (Time.time >= nextStateLog)
                {
                    nextStateLog = Time.time + 6f;
                    Debug.Log("[Barata] luz fora do jogo: " + (beam == null ? "não encontrada" : "ligada=" + beam.enabled + ", objeto ativo=" + beam.gameObject.activeInHierarchy));
                }
                return false;
            }
            Vector3 p = transform.position + Vector3.up * 0.02f;
            Camera cam = Camera.main;

            float range = Mathf.Max(beam.range, 14f);
            bool inBeam = false;
            Vector3 toB = p - beam.transform.position;
            float dB = toB.magnitude;
            if (dB > 1e-3f && dB <= range)
            {
                float cone = (beam.type == LightType.Spot ? beam.spotAngle * 0.5f : 180f) + 8f;
                inBeam = Vector3.Angle(beam.transform.forward, toB) <= cone && Clear(beam.transform.position, toB, dB);
            }

            bool inAim = false;
            float dC = 0f, angC = 999f;
            if (cam != null)
            {
                Vector3 toC = p - cam.transform.position;
                dC = toC.magnitude;
                angC = Vector3.Angle(cam.transform.forward, toC);
                inAim = dC <= range && angC <= 20f && Clear(cam.transform.position, toC, dC);
            }

            if (Time.time >= nextDebug && cam != null && dC < 16f)
            {
                nextDebug = Time.time + 2f;
                Debug.Log("[Barata] luz ligada, a " + dC.ToString("F1") + " m, " + angC.ToString("F0") + "° do centro da tela; no feixe=" + inBeam + ", na mira=" + inAim + " -> " + (inBeam || inAim ? "FOGE" : "parada"));
            }
            return inBeam || inAim;
        }

        bool Clear(Vector3 from, Vector3 to, float dist)
        {
            Vector3 dir = to / dist;
            foreach (RaycastHit h in Physics.RaycastAll(from, dir, Mathf.Max(0f, dist - 0.06f), ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(transform)) continue;
                if (h.transform.GetComponentInParent<Camera>() != null) continue;
                if (beam != null && h.transform.IsChildOf(beam.transform.root)) continue;
                return false;
            }
            return true;
        }

        void PickFleeDirection()
        {
            Vector3 away = beam != null ? transform.position - beam.transform.position : transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = Random.insideUnitSphere;
            away.y = 0f;
            dir = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * away.normalized;
        }

        void Idle()
        {
            if (anim != null) anim.speed = Time.time < twitchUntil ? 1.2f : 0f;
            if (Time.time >= nextTwitch)
            {
                nextTwitch = Time.time + Random.Range(twitchEvery * 0.5f, twitchEvery * 1.6f);
                twitchUntil = Time.time + Random.Range(0.25f, 0.6f);
                if (Random.value < 0.5f)
                {
                    shuffleUntil = Time.time + Random.Range(0.2f, 0.5f);
                    dir = Quaternion.Euler(0f, Random.Range(-120f, 120f), 0f) * dir;
                }
            }
            if (Time.time < shuffleUntil) Move(runSpeed * 0.35f, 1.1f);
        }

        void Move(float speed, float animSpeed)
        {
            if (anim != null) anim.speed = animSpeed;
            Vector3 p = transform.position;
            Vector3 chest = p + Vector3.up * 0.02f;

            for (int i = 0; i < 6; i++)
            {
                if (!Physics.Raycast(chest, dir, obstacleCheck, ~0, QueryTriggerInteraction.Ignore)) break;
                dir = Quaternion.Euler(0f, (Random.value < 0.5f ? -1f : 1f) * Random.Range(50f, 130f), 0f) * dir;
            }

            Vector3 next = p + dir * speed * Time.deltaTime;
            if (Physics.Raycast(next + Vector3.up * 0.3f, Vector3.down, out RaycastHit ground, 0.8f, ~0, QueryTriggerInteraction.Ignore) && ground.normal.y > 0.5f)
                transform.position = new Vector3(next.x, ground.point.y, next.z);
            else
                dir = Quaternion.Euler(0f, Random.Range(120f, 240f), 0f) * dir;

            Quaternion want = Quaternion.LookRotation(dir, Vector3.up) * headFix;
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }
    }
}
