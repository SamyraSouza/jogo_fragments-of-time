using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RiftOfTime
{
    /// <summary>Combate em primeira pessoa (storyboard: "golpear com a adaga [Clique] e esquivar dos ataques").</summary>
    [DefaultExecutionOrder(200)]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Ataque")]
        public float damage = 34f;
        public float reach = 2.6f;
        public float arcDegrees = 110f;
        public float attackCooldown = 0.55f;

        [Header("Esquiva")]
        public float dodgeSpeed = 13f;
        public float dodgeDuration = 0.22f;
        public float dodgeCooldown = 0.9f;

        Camera cam;
        Transform dagger;
        Transform arms;
        Animator armsAnim;
        PunchAnimator punch;
        Rigidbody rb;
        PlayerHealth health;
        float nextAttack, nextDodge, dashLeft;
        Vector3 dashDir;
        bool attacking;

        static readonly int AttackState = Animator.StringToHash("Attack");
        const float ArmsHitDelay = 0.2f, ArmsRecover = 0.25f;

        static readonly Vector3 RestPos = new Vector3(0.34f, -0.32f, 0.55f);
        static readonly Quaternion RestRot = Quaternion.Euler(-38f, -14f, 6f);
        static readonly Vector3 WindPos = new Vector3(0.42f, -0.22f, 0.42f);
        static readonly Quaternion WindRot = Quaternion.Euler(-62f, 28f, -18f);
        static readonly Vector3 SlashPos = new Vector3(0.05f, -0.28f, 0.62f);
        static readonly Quaternion SlashRot = Quaternion.Euler(-8f, -52f, 26f);

        void Start()
        {
            cam = GetComponentInChildren<Camera>(true);
            rb = GetComponent<Rigidbody>();
            health = GetComponent<PlayerHealth>();
            if (cam != null)
            {
                arms = cam.transform.Find("Arms");
                if (arms == null)
                    foreach (Transform t in GetComponentsInChildren<Transform>(true))
                        if (t.name == "Arms") { arms = t; break; }
                if (arms != null) armsAnim = arms.GetComponentInChildren<Animator>(true);
                if (armsAnim != null)
                {
                    punch = armsAnim.GetComponent<PunchAnimator>();
                    if (punch == null) punch = armsAnim.gameObject.AddComponent<PunchAnimator>();
                }
                BuildDagger();
            }
            Debug.Log("[Combat] iniciado. câmera=" + (cam != null) + " Arms=" + (arms != null) + " Animator=" + (armsAnim != null) + " punch=" + (punch != null));
        }

        void BuildDagger()
        {
            if (arms != null)
            {
                Transform loose = cam.transform.Find("Dagger");
                if (loose != null) loose.gameObject.SetActive(false);
                if (PlayerLoadout.DaggerPhase && arms.GetComponent<HeldDagger>() == null) arms.gameObject.AddComponent<HeldDagger>();
                return;
            }

            Transform placed = cam.transform.Find("Dagger");
            if (placed != null)
            {
                dagger = placed;
                dagger.localPosition = RestPos;
                dagger.localRotation = RestRot;
                return;
            }

            var root = new GameObject("Dagger").transform;
            root.SetParent(cam.transform, false);
            root.localPosition = RestPos;
            root.localRotation = RestRot;

            Material steel = RotMaterials.Lit(new Color(0.78f, 0.8f, 0.84f));
            Material gold = RotMaterials.Lit(new Color(0.85f, 0.65f, 0.2f));
            Material leather = RotMaterials.Lit(new Color(0.28f, 0.16f, 0.1f));

            ProcBuild.Cube(root, "Blade", new Vector3(0f, 0f, 0.26f), new Vector3(0.05f, 0.012f, 0.42f), steel);
            ProcBuild.Cube(root, "Tip", new Vector3(0f, 0f, 0.5f), new Vector3(0.03f, 0.01f, 0.08f), steel);
            ProcBuild.Cube(root, "Guard", new Vector3(0f, 0f, 0.03f), new Vector3(0.17f, 0.03f, 0.03f), gold);
            ProcBuild.Cube(root, "Handle", new Vector3(0f, 0f, -0.07f), new Vector3(0.035f, 0.035f, 0.16f), leather);
            ProcBuild.Cube(root, "Pommel", new Vector3(0f, 0f, -0.16f), new Vector3(0.05f, 0.05f, 0.04f), gold);
            dagger = root;
        }

        void Update()
        {
            bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            bool pressed = clicked || space;

            if (Time.timeScale <= 0f || (health != null && health.IsDead))
            {
                if (pressed) Debug.Log("[Combat] ignorado: jogo pausado ou jogador morto");
                return;
            }
            if (Cursor.visible && Cursor.lockState != CursorLockMode.Locked)
            {
                if (pressed) Debug.Log("[Combat] ignorado: cursor livre (menu/pausa aberto?)");
                return;
            }

            if (pressed)
            {
                if (Time.time < nextAttack || attacking) { /* em cooldown */ }
                else if (dagger == null && arms == null) Debug.Log("[Combat] ignorado: não achei o filho 'Arms' na câmera do Player");
                else
                {
                    Debug.Log("[Combat] golpe! punch pronto: " + (punch != null && punch.Ready));
                    nextAttack = Time.time + attackCooldown;
                    StartCoroutine(Swing());
                }
            }

            if (Keyboard.current != null &&
                (Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.leftCtrlKey.wasPressedThisFrame))
                TryDodge();
        }

        IEnumerator Swing()
        {
            attacking = true;
            AudioDirector.Sfx("slash", 0.7f);
            if (arms != null)
            {
                if (punch != null && punch.Ready) punch.Play();
                else if (armsAnim != null && armsAnim.HasState(0, AttackState)) armsAnim.Play(AttackState, 0, 0f);
                yield return new WaitForSeconds(ArmsHitDelay);
                DoHit();
                yield return new WaitForSeconds(ArmsRecover);
            }
            else
            {
                yield return Tween(RestPos, RestRot, WindPos, WindRot, 0.08f);
                DoHit();
                yield return Tween(WindPos, WindRot, SlashPos, SlashRot, 0.10f);
                yield return Tween(SlashPos, SlashRot, RestPos, RestRot, 0.18f);
            }
            attacking = false;
        }

        IEnumerator Tween(Vector3 p0, Quaternion r0, Vector3 p1, Quaternion r1, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                dagger.localPosition = Vector3.Lerp(p0, p1, k);
                dagger.localRotation = Quaternion.Slerp(r0, r1, k);
                yield return null;
            }
            dagger.localPosition = p1;
            dagger.localRotation = r1;
        }

        void DoHit()
        {
            Vector3 origin = cam.transform.position;
            Vector3 fwd = cam.transform.forward;
            MummyGuardian[] all = MummyGuardian.All.ToArray();
            for (int i = 0; i < all.Length; i++)
            {
                MummyGuardian m = all[i];
                if (m == null || m.IsDead) continue;
                Vector3 to = (m.transform.position + Vector3.up * 1.0f) - origin;
                if (to.magnitude > reach + 0.4f) continue;
                if (Vector3.Angle(fwd, to) > arcDegrees * 0.5f) continue;
                m.TakeDamage(damage, fwd);
                CameraShake.Kick(0.03f, 0.12f);
            }
        }

        void TryDodge()
        {
            if (Time.time < nextDodge || dashLeft > 0f) return;

            Vector3 dir = Vector3.zero;
            Keyboard k = Keyboard.current;
            if (k.wKey.isPressed) dir += transform.forward;
            if (k.sKey.isPressed) dir -= transform.forward;
            if (k.dKey.isPressed) dir += transform.right;
            if (k.aKey.isPressed) dir -= transform.right;
            if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;
            dir.y = 0f;
            dir.Normalize();

            dashDir = dir;
            dashLeft = dodgeDuration;
            nextDodge = Time.time + dodgeCooldown;
            StartCoroutine(DodgeFx());
        }

        IEnumerator DodgeFx()
        {
            if (health != null) health.Invulnerable = true;
            float baseFov = cam != null ? cam.fieldOfView : 60f;
            float t = 0f;
            float total = dodgeDuration + 0.12f;
            while (t < total)
            {
                t += Time.deltaTime;
                if (cam != null) cam.fieldOfView = baseFov + Mathf.Sin(Mathf.Clamp01(t / total) * Mathf.PI) * 8f;
                yield return null;
            }
            if (cam != null) cam.fieldOfView = baseFov;
            if (health != null) health.Invulnerable = false;
        }

        void FixedUpdate()
        {
            if (dashLeft <= 0f || rb == null) return;
            float dist = dodgeSpeed * Time.fixedDeltaTime;
            RaycastHit hit;
            if (rb.SweepTest(dashDir, out hit, dist + 0.05f, QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0f, hit.distance - 0.05f);
            rb.MovePosition(rb.position + dashDir * dist);
            dashLeft -= Time.fixedDeltaTime;
        }
    }
}
