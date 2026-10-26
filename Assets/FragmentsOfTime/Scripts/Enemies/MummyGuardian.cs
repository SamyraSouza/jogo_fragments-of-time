using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RiftOfTime
{
    /// <summary>Múmia Guardiã (Fase 1) e Faraó (Fase 2).</summary>
    [RequireComponent(typeof(CharacterController))]
    public class MummyGuardian : MonoBehaviour
    {
        public static readonly List<MummyGuardian> All = new List<MummyGuardian>();

        public enum State { Dormant, Awakening, Chase, Windup, Strike, Recover, Dead }

        [Header("Identidade")]
        public string displayName = "Guardiã I";
        public bool isPharaoh;

        [Header("Atributos")]
        public float maxHealth = 100f;
        public float moveSpeed = 2.4f;
        public float turnSpeed = 6f;
        public float awakenRange = 11f;
        public float attackRange = 1.9f;
        public float attackDamage = 15f;
        public float windupTime = 0.75f;
        public float recoverTime = 1.1f;

        public float Health { get; private set; }
        public bool IsDead { get { return state == State.Dead; } }
        public State Current { get { return state; } }

        /// <summary>Opcional: dado (de, para) devolve o próximo ponto do caminho.</summary>
        public Func<Vector3, Vector3, Vector3> Pathfinder;

        public event Action<MummyGuardian> Awakened;
        public event Action<MummyGuardian> Died;
        public event Action<float> HealthChanged;

        State state = State.Dormant;
        Transform target;
        float groundY;
        PlayerHealth targetHealth;
        CharacterController cc;
        MummyBuilder.Rig rig;
        Color[] baseColors;
        float timer, knock, animT;
        Vector3 knockDir;
        bool sweepNext;
        bool sweepAttack;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Awake()
        {
            Health = maxHealth;
            cc = GetComponent<CharacterController>();
            float scale = isPharaoh ? 1.12f : 1f;
            cc.height = 1.9f * scale;
            cc.radius = 0.55f * scale;
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            cc.stepOffset = 0.05f;      // não sobe em saliências da base das paredes
            groundY = transform.position.y;

            Transform existing = transform.Find("Model");
            if (existing == null)
            {
                rig = MummyBuilder.Build(transform, isPharaoh);
                rig.root.localScale = Vector3.one * scale;
                baseColors = new Color[rig.renderers.Count];
                for (int i = 0; i < baseColors.Length; i++) baseColors[i] = RotMaterials.GetColor(rig.renderers[i].sharedMaterial);
            }
            SetEyes(false);
        }

        void SetEyes(bool on)
        {
            if (rig == null) return;
            foreach (Renderer e in rig.eyes) e.enabled = on;
        }

        void Update()
        {
            if (state == State.Dead) return;

            if (target == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) target = p.transform;
                if (target == null) { cc.SimpleMove(Vector3.zero); return; }
            }

            if (targetHealth == null) targetHealth = target.GetComponent<PlayerHealth>();
            if (targetHealth != null && targetHealth.IsDead) { cc.SimpleMove(Vector3.zero); return; }   // jogador morto: a múmia para

            float dt = Time.deltaTime;
            Vector3 before = transform.position;
            Vector3 to = target.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 move = Vector3.zero;
            animT += dt;

            switch (state)
            {
                case State.Dormant:
                    PoseDormant();
                    if (dist < awakenRange) BeginAwaken();
                    break;

                case State.Awakening:
                    Face(to, dt * 2.2f);               // vira para o jogador antes de gritar
                    timer -= dt;
                    PoseAwakening(1f - Mathf.Clamp01(timer / 1.2f));
                    if (timer <= 0f) state = State.Chase;
                    break;

                case State.Chase:
                    Vector3 aim = to;
                    if (Pathfinder != null)
                    {
                        Vector3 wp = Pathfinder(transform.position, target.position) - transform.position;
                        wp.y = 0f;
                        if (wp.sqrMagnitude > 0.04f) aim = wp;
                    }
                    Face(aim, dt * 2.5f);               // vira de frente para onde vai andar
                    bool inReach = dist <= attackRange * 0.9f && HasLineOfSight();
                    // só avança na direção em que está olhando: de lado ela primeiro gira, depois anda
                    float aligned = Mathf.Clamp01((Vector3.Dot(transform.forward, aim.normalized) - 0.2f) / 0.6f);
                    if (!inReach) move = transform.forward * moveSpeed * aligned;
                    else BeginWindup();
                    PoseWalk();
                    break;

                case State.Windup:
                    Face(to, dt * 0.35f);
                    timer -= dt;
                    PoseWindup(1f - Mathf.Clamp01(timer / windupTime));
                    if (timer <= 0f) DoStrike(dist, to);
                    break;

                case State.Strike:
                    timer -= dt;
                    PoseStrike(1f - Mathf.Clamp01(timer / 0.35f));
                    if (timer <= 0f) { state = State.Recover; timer = recoverTime; sweepAttack = false; }
                    break;

                case State.Recover:
                    timer -= dt;
                    PoseWalk();
                    if (timer <= 0f) state = State.Chase;
                    break;
            }

            if (knock > 0.01f)
            {
                cc.Move(knockDir * knock * dt);
                knock = Mathf.MoveTowards(knock, 0f, 10f * dt);
            }
            cc.SimpleMove(move);
            KeepOutOfWalls(before);
            if (Mathf.Abs(transform.position.y - groundY) > 0.02f)     // o chão é plano: nunca fica mais alta que o chão em que nasceu
            {
                cc.enabled = false;
                Vector3 pp = transform.position; pp.y = groundY; transform.position = pp;
                cc.enabled = true;
            }
        }

        // rede de segurança: se o passo desta volta cruzou o colisor de uma parede, volta para onde estava
        void KeepOutOfWalls(Vector3 before)
        {
            Vector3 a = before + Vector3.up * 0.9f, b = transform.position + Vector3.up * 0.9f;
            if ((b - a).sqrMagnitude < 0.0001f) return;
            RaycastHit hit;
            if (!Physics.Linecast(a, b, out hit, ~0, QueryTriggerInteraction.Ignore)) return;
            Transform h = hit.collider.transform;
            if (h.IsChildOf(transform) || (target != null && (h.IsChildOf(target) || target.IsChildOf(h)))) return;
            cc.enabled = false;
            transform.position = before;
            cc.enabled = true;
        }

        /// <summary>Sem parede entre a múmia e o jogador (no labirinto, o golpe não atravessa paredes).</summary>
        bool HasLineOfSight()
        {
            Vector3 a = transform.position + Vector3.up;
            Vector3 b = target.position + Vector3.up;
            RaycastHit hit;
            if (!Physics.Linecast(a, b, out hit, ~0, QueryTriggerInteraction.Ignore)) return true;
            Transform h = hit.collider.transform;
            return h.IsChildOf(target) || h.IsChildOf(transform) || target.IsChildOf(h);
        }

        void Face(Vector3 to, float dt)
        {
            if (to.sqrMagnitude < 0.0001f) return;
            Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Mathf.Clamp01(turnSpeed * dt));
        }

        /// <summary>Faz a múmia sair do sono e já andar na direção do jogador (sem a demora do despertar).</summary>
        public void StartChasing()
        {
            if (state != State.Dormant && state != State.Awakening) return;
            state = State.Chase;
            SetEyes(true);
            AudioDirector.Sfx("groan", 0.7f);
        }

        void BeginAwaken()
        {
            state = State.Awakening;
            timer = 1.5f;
            SetEyes(true);
            AudioDirector.Sfx("groan", 0.9f);
            if (Awakened != null) Awakened(this);
        }

        void BeginWindup()
        {
            state = State.Windup;
            sweepAttack = isPharaoh && sweepNext;
            if (isPharaoh) sweepNext = !sweepNext;
            timer = sweepAttack ? windupTime + 0.25f : windupTime;
        }

        void DoStrike(float dist, Vector3 to)
        {
            state = State.Strike;
            timer = 0.35f;
            if (targetHealth == null || !targetHealth.IsDead) AudioDirector.Sfx("mummy_hit", 0.9f);

            float range = sweepAttack ? attackRange + 1.0f : attackRange + 0.45f;
            bool inFront = Vector3.Dot(transform.forward, to.normalized) > 0.25f;
            if (dist <= range && (sweepAttack || inFront) && HasLineOfSight())
            {
                PlayerHealth ph = target.GetComponent<PlayerHealth>();
                if (ph != null) ph.TakeDamage(sweepAttack ? attackDamage * 1.4f : attackDamage);
            }
        }

        public void TakeDamage(float amount, Vector3 hitDir)
        {
            if (state == State.Dead) return;
            if (state == State.Dormant) BeginAwaken();

            Health = Mathf.Max(0f, Health - amount);
            if (HealthChanged != null) HealthChanged(Health / maxHealth);
            AudioDirector.Sfx("hit", 0.8f);
            StartCoroutine(HitFlash());

            hitDir.y = 0f;
            knockDir = hitDir.sqrMagnitude > 0.001f ? hitDir.normalized : transform.forward * -1f;
            knock = 0f;     // apanhar não empurra a múmia para longe

            if (Health <= 0f) StartCoroutine(Die());
        }

        IEnumerator HitFlash()
        {
            if (rig == null) yield break;
            for (int i = 0; i < rig.renderers.Count; i++)
                RotMaterials.SetColor(rig.renderers[i].material, new Color(1f, 0.3f, 0.25f));
            yield return new WaitForSeconds(0.09f);
            for (int i = 0; i < rig.renderers.Count; i++)
                if (rig.renderers[i] != null) RotMaterials.SetColor(rig.renderers[i].material, baseColors[i]);
        }

        IEnumerator Die()
        {
            state = State.Dead;
            cc.enabled = false;
            SetEyes(false);
            AudioDirector.Sfx("groan", 1f);
            if (Died != null) Died(this);

            if (rig == null) yield break;
            Quaternion from = rig.root.localRotation;
            Quaternion to = Quaternion.Euler(-90f, 0f, 0f);
            float t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime;
                float k = t / 0.9f;
                rig.root.localRotation = Quaternion.Slerp(from, to, k * k);
                yield return null;
            }
            CameraShake.Kick(0.05f, 0.25f);
        }

        void PoseDormant()
        {
            if (rig == null) return;
            rig.head.localRotation = Quaternion.Euler(38f, 0f, 0f);
            rig.torso.localRotation = Quaternion.Euler(8f, 0f, 0f);
            if (isPharaoh)
            {
                rig.armL.localRotation = Quaternion.Euler(-62f, 22f, 0f);
                rig.armR.localRotation = Quaternion.Euler(-62f, -22f, 0f);
            }
            else
            {
                rig.armL.localRotation = Quaternion.identity;
                rig.armR.localRotation = Quaternion.identity;
            }
        }

        void PoseAwakening(float k)
        {
            if (rig == null) return;
            rig.head.localRotation = Quaternion.Euler(Mathf.Lerp(38f, 0f, k), 0f, 0f);
            rig.torso.localRotation = Quaternion.Euler(Mathf.Lerp(8f, 0f, k), 0f, 0f);
            float armX = Mathf.Lerp(isPharaoh ? -62f : 0f, -75f, k);
            rig.armL.localRotation = Quaternion.Euler(armX, Mathf.Lerp(isPharaoh ? 22f : 0f, 0f, k), 0f);
            rig.armR.localRotation = Quaternion.Euler(armX, Mathf.Lerp(isPharaoh ? -22f : 0f, 0f, k), 0f);
        }

        void PoseWalk()
        {
            if (rig == null) return;
            float s = Mathf.Sin(animT * 6f);
            rig.root.localPosition = new Vector3(0f, Mathf.Abs(s) * 0.05f, 0f);
            rig.armL.localRotation = Quaternion.Euler(-75f + s * 8f, 0f, 0f);
            rig.armR.localRotation = Quaternion.Euler(-75f - s * 8f, 0f, 0f);
            rig.torso.localRotation = Quaternion.Euler(6f, s * 4f, 0f);
            rig.head.localRotation = Quaternion.identity;
        }

        void PoseWindup(float k)
        {
            if (rig == null) return;
            if (sweepAttack)
            {
                rig.armL.localRotation = Quaternion.Euler(-90f, 0f, Mathf.Lerp(0f, 80f, k));
                rig.armR.localRotation = Quaternion.Euler(-90f, 0f, Mathf.Lerp(0f, -80f, k));
                rig.torso.localRotation = Quaternion.Euler(0f, Mathf.Lerp(0f, -50f, k), 0f);
            }
            else
            {
                float x = Mathf.Lerp(-75f, -170f, k);
                rig.armL.localRotation = Quaternion.Euler(x, 0f, 0f);
                rig.armR.localRotation = Quaternion.Euler(x, 0f, 0f);
                rig.torso.localRotation = Quaternion.Euler(Mathf.Lerp(6f, -12f, k), 0f, 0f);
            }
        }

        void PoseStrike(float k)
        {
            if (rig == null) return;
            if (sweepAttack)
            {
                rig.torso.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-50f, 360f, k), 0f);
            }
            else
            {
                float x = Mathf.Lerp(-170f, -20f, Mathf.Clamp01(k * 2.5f));
                rig.armL.localRotation = Quaternion.Euler(x, 0f, 0f);
                rig.armR.localRotation = Quaternion.Euler(x, 0f, 0f);
                rig.torso.localRotation = Quaternion.Euler(Mathf.Lerp(-12f, 16f, Mathf.Clamp01(k * 2.5f)), 0f, 0f);
            }
        }
    }
}
